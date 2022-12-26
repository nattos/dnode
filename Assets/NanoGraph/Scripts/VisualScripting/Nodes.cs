using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;
using Unity.VisualScripting;

namespace NanoGraph.VisualScripting {
  public abstract class BaseNode : Unit, IDCustomInspectorDataProvider, IValueEditedHandler {
    [Serialize]
    public IDataNode Node;

    private readonly Dictionary<string, IUnitInputPort> _fieldNameToInputPortMap = new Dictionary<string, IUnitInputPort>();

    [Serialize]
    private readonly Dictionary<string, LiteralNode> _fieldNameToLiteralNodes = new Dictionary<string, LiteralNode>();
    [NonSerialized]
    private bool _didConnectSerializedLiteralNodes = false;
    private readonly Dictionary<string, DCustomInspectorData> _fieldNameToCustomInspectorData = new Dictionary<string, DCustomInspectorData>();

    private readonly Action _nodeInvalidatedHandler;
    private bool _isRegisteredAsOutputNode;

    public BaseNode() {
      _nodeInvalidatedHandler = () => { PortsChanged(); Define(); SyncInputConnectionsToGraphEdges(); };
    }

    public override void BeforeAdd() {
      base.BeforeAdd();
      EnsureNode();
      Node.Graph.AddNodeInvalidatedHandler(Node, _nodeInvalidatedHandler);
    }

    public override void AfterAdd() {
      base.AfterAdd();
      SyncInputConnectionsToGraphEdges();
    }

    public override void AfterRemove() {
      base.AfterRemove();
      // TODO: Remove node.
      Node?.Graph?.RemoveNode(Node);
      foreach (LiteralNode node in _fieldNameToLiteralNodes.Values) {
        node?.Graph?.RemoveNode(node);
      }
      _fieldNameToLiteralNodes.Clear();

      Node?.Graph?.AddNodeInvalidatedHandler(Node, _nodeInvalidatedHandler);
    }

    public void NotifyInputConnectionsChanged() {
      SyncInputConnectionsToGraphEdges();
      (Node as IAutoTypeNode)?.UpdateTypesFromInputs();
    }

    public void NotifyOutputConnectionsChanged() {
      SyncOutputNodeRegistration();
    }

    void IValueEditedHandler.OnValueEdited(string fieldName) {
      if (_fieldNameToLiteralNodes.TryGetValue(fieldName, out LiteralNode literalNode)) {
        Plugin.PluginService.Instance.SetDebugValue(literalNode.DebugValueKey, literalNode.EffectiveInternalValue.ValueArray);
        return;
      }
      Node?.Graph?.CompileLater();
    }

    protected override void Definition() {
      EnsureNode();

      _fieldNameToCustomInspectorData.Clear();
      DataSpec inputSpec = Node.InputSpec;
      DataSpec outputSpec = Node.OutputSpec;
      foreach (var field in inputSpec.Fields) {
        DValue? value = null;
        if (field.Type.Primitive != null) {
          switch (field.Type.Primitive) {
            case PrimitiveType.Bool:
            case PrimitiveType.Int:
            case PrimitiveType.Uint:
            case PrimitiveType.Float:
              value = (DValue)0;
              break;
            case PrimitiveType.Uint2:
            case PrimitiveType.Float2:
              value = Vector2.zero;
              break;
            case PrimitiveType.Float3:
              value = Vector3.zero;
              break;
            case PrimitiveType.Float4:
              value = Vector4.zero;
              break;
            case PrimitiveType.TypeDecl:
            case PrimitiveType.Vertices:
            default:
              break;
          }
        }
        if (value == null) {
          ValueInput<DValue>(field.Name);
        } else {
          ValueInput<DValue>(field.Name, value.Value);
        }
        _fieldNameToCustomInspectorData[field.Name] = new DCustomInspectorData {
          DefaultValue = value,
          Dimensions = value?.Columns,
          MinValue = 0.0,
          MaxValue = 1.0,
        };
      }
      foreach (var field in outputSpec.Fields) {
        ValueOutput<DValue>(field.Name);
      }

      _fieldNameToInputPortMap.Clear();
      foreach (var input in inputs) {
        _fieldNameToInputPortMap[input.key] = input;
      }
      Node.Graph.MarkNodeDirty(Node);
      Node.Graph.ValidateLater();
    }

    public DCustomInspectorData? ProvideCustomInspectorData(string key) {
      if (_fieldNameToCustomInspectorData.TryGetValue(key, out var data)) {
        return data;
      }
      return null;
    }

    private void EnsureNode() {
      if (Node != null) {
        if (Node.Graph == null) {
          Node.DebugId = this.guid.ToString();
          NanoGraph.DebugInstance.AddNode(Node);
        }
        return;
      }
      Node = CreateNode();
      Node.DebugId = this.guid.ToString();
      NanoGraph.DebugInstance.AddNode(Node);
    }

    protected abstract IDataNode CreateNode();

    public void SyncInputConnectionsToGraphEdges() {
      // Debug.Log($"Syncing Input: {this}");
      NanoGraph graph = Node.Graph;
      DataEdge[] oldInputEdges = graph.GetInputEdges(Node);

      DataSpec inputSpec = Node.InputSpec;
      List<DataEdge> edgesToRemove = new List<DataEdge>();
      List<DataEdge> edgesToAdd = new List<DataEdge>();

      // Look for disconnected or changed inputs.
      foreach (DataEdge oldInputEdge in oldInputEdges) {
        IDataNode oldSourceNode = oldInputEdge.Source.Node;
        if (!_fieldNameToInputPortMap.TryGetValue(oldInputEdge.Destination.FieldName, out IUnitInputPort inputPort)) {
          edgesToRemove.Add(oldInputEdge);
          continue;
        }
        var inputConnection = inputPort.connections.FirstOrDefault();
        if (inputConnection == null || inputConnection.source == null) {
          // Do not remove if this is our own literal node.
          if (_fieldNameToLiteralNodes.TryGetValue(inputPort.key, out LiteralNode node)) {
            if (oldSourceNode == node) {
              continue;
            }
          }
          edgesToRemove.Add(oldInputEdge);
          continue;
        }
        IDataNode newSourceNode = GetSourceBaseNodeOrNull(inputConnection.source.unit, inputConnection.source.key, out string actualKey)?.Node;
        if (oldSourceNode != newSourceNode || actualKey != oldInputEdge.Source.FieldName) {
          edgesToRemove.Add(oldInputEdge);
          edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = newSourceNode, FieldName = actualKey }, Destination = oldInputEdge.Destination });
          continue;
        }
      }
      // Look for new connections.
      foreach (IUnitInputPort inputPort in inputs) {
        var inputConnection = inputPort.connections.FirstOrDefault();
        IDataNode inputNode = GetSourceBaseNodeOrNull(inputConnection?.source.unit, inputConnection?.source.key, out string actualKey)?.Node;
        if (inputNode == null) {
          // TODO: Update nodes if types differ.
          // Link a LiteralNode if appropriate.
          bool relinkNode = false;
          DataField? field = inputSpec.Fields.FirstOrNull(field => field.Name == inputPort.key);
          if (field?.Type.Primitive == null) {
            continue;
          }
          PrimitiveType fieldPrimitiveType = (field?.Type.Primitive).Value;
          if (_fieldNameToLiteralNodes.TryGetValue(inputPort.key, out LiteralNode literalNode)) {
            if (literalNode.Type != fieldPrimitiveType) {
              RemoveLiteralNode(new KeyValuePair<string, LiteralNode>(inputPort.key, literalNode));
              relinkNode = true;
            } else {
              if (!_didConnectSerializedLiteralNodes) {
                graph.AddNode(literalNode);
                edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = literalNode, FieldName = "Out" }, Destination = new DataPlug { Node = Node, FieldName = inputPort.key } });
              }
            }
          } else {
            relinkNode = true;
          }
          if (relinkNode) {
            bool isArray = field?.Type.IsArray ?? false;
            Func<DValue> valueProvider = () => defaultValues.GetValueOrDefault(inputPort.key) as DValue? ?? default;
            literalNode = new LiteralNode { SerializedName = "Value", Type = fieldPrimitiveType, ValueSource = InputSource.Internal, InternalValueProvider = valueProvider };
            _fieldNameToLiteralNodes[inputPort.key] = literalNode;

            graph.AddNode(literalNode);
            edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = literalNode, FieldName = "Out" }, Destination = new DataPlug { Node = Node, FieldName = inputPort.key } });
          }
          continue;
        }
        // Input is connected. Check if it is already mapped.
        if (oldInputEdges.Any(edge => edge.Destination.FieldName == inputPort.key)) {
          continue;
        }
        // Input is connected and not in the existing edges. Add it.
        edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = inputNode, FieldName = actualKey }, Destination = new DataPlug { Node = Node, FieldName = inputPort.key } });
      }

      foreach (var entry in _fieldNameToLiteralNodes.ToArray()) {
        if (!_fieldNameToInputPortMap.TryGetValue(entry.Key, out IUnitInputPort inputPort)) {
          RemoveLiteralNode(entry);
          continue;
        }
        bool isConnected = inputPort.connections.Any();
        if (isConnected) {
          RemoveLiteralNode(entry);
          continue;
        }
      }

      // Commit changes.
      foreach (DataEdge toRemove in edgesToRemove) {
        graph.Disconnect(toRemove.Source.Node, toRemove.Source.FieldName, toRemove.Destination.Node, toRemove.Destination.FieldName);
      }
      foreach (DataEdge toAdd in edgesToAdd) {
        graph.Connect(toAdd.Source.Node, toAdd.Source.FieldName, toAdd.Destination.Node, toAdd.Destination.FieldName);
      }

      _didConnectSerializedLiteralNodes = true;
    }

    private void ClearLiteralNodes() {
      foreach (var entry in _fieldNameToLiteralNodes.ToArray()) {
        RemoveLiteralNode(entry);
      }
    }

    private void RemoveLiteralNode(KeyValuePair<string, LiteralNode> entry) {
      _fieldNameToLiteralNodes.Remove(entry.Key);
      entry.Value?.Graph?.RemoveNode(entry.Value);
    }

    private void SyncOutputNodeRegistration() {
      bool isOutputNode = false;
      foreach (var output in outputs) {
        foreach (var connection in output.connections) {
          if (connection.destination.unit is GraphOutput graphOutput) {
            isOutputNode = true;
            break;
          }
        }
      }

      if (_isRegisteredAsOutputNode != isOutputNode) {
        _isRegisteredAsOutputNode = isOutputNode;
        IComputeNode computeNode = Node as IComputeNode;
        if (computeNode != null) {
          if (isOutputNode) {
            Node.Graph.RegisterOutputNode(computeNode);
          } else {
            Node.Graph.UnregisterOutputNode(computeNode);
          }
        }
      }
    }

    public static IEnumerable<BaseNode> GetDestBaseNodes(IUnit dest, string key) {
      if (dest is SubgraphUnit subgraphUnit) {
        GraphInput graphInput = subgraphUnit.nest.graph.units.FirstOrDefault(unit => unit is GraphInput) as GraphInput;
        if (graphInput == null) {
          yield break;
        }
        foreach (var connection in graphInput.connections) {
          if (connection.source.key == key) {
            if (connection.destination.unit is BaseNode baseNode) {
              yield return baseNode;
            }
          }
        }
        yield break;
      } else if (dest is BaseNode baseNode) {
        yield return baseNode;
      }
    }

    public static BaseNode GetSourceBaseNodeOrNull(IUnit src, string key, out string actualKey) {
      actualKey = key;
      if (src is SubgraphUnit subgraphUnit) {
        GraphOutput graphOutput = subgraphUnit.nest.graph.units.FirstOrDefault(unit => unit is GraphOutput) as GraphOutput;
        if (graphOutput == null) {
          return null;
        }
        foreach (var connection in graphOutput.connections) {
          if (connection.destination.key == key) {
            if (connection.source.unit is BaseNode baseNode) {
              return baseNode;
            }
          }
        }
        return null;
      } else if (src is GraphInput graphInput) {
        SubgraphUnit ownerSubgraphUnit = GetSubgraphUnit(graphInput.graph);
        if (ownerSubgraphUnit == null) {
          return null;
        }
        foreach (var connection in ownerSubgraphUnit.connections) {
          if (connection.destination.key == key) {
            return GetSourceBaseNodeOrNull(connection.source.unit, connection.source.key, out actualKey);
          }
        }
      } else if (src is BaseNode baseNode) {
        return baseNode;
      }
      return null;
    }

    private static readonly List<(WeakReference, SubgraphUnit)> _weakNestedGraphs = new List<(WeakReference, SubgraphUnit)>();

    public static SubgraphUnit GetSubgraphUnit(FlowGraph nestedGraph) {
      foreach ((WeakReference graphRef, SubgraphUnit unitEntry) in _weakNestedGraphs) {
        if (graphRef.Target is FlowGraph graphEntry && graphEntry == nestedGraph) {
          return unitEntry;
        }
      }
      return null;
    }

    public static void MapSubgraphUnit(SubgraphUnit subgraphUnit) {
      FlowGraph graph = subgraphUnit.nest.graph;
      bool sweepNulls = false;
      foreach ((WeakReference graphRef, SubgraphUnit unitEntry) in _weakNestedGraphs) {
        if (graphRef.Target is FlowGraph graphEntry) {
          if (graphEntry == graph) {
            break;
          }
        } else {
          sweepNulls = true;
        }
      }
      _weakNestedGraphs.Add((new WeakReference(graph), subgraphUnit));
      if (sweepNulls) {
        _weakNestedGraphs.RemoveAll(entry => !entry.Item1.IsAlive);
      }
    }
  }

  public abstract class NodeOfType<T> : BaseNode where T : IDataNode, new() {
    protected override IDataNode CreateNode() => new T();
  }



  public class VectorCompute : NodeOfType<VectorComputeNode> {}
  public class ScalarCompute : NodeOfType<ScalarComputeNode> {}
  public class VertexCompute : NodeOfType<VertexShaderComputeNode> {}
  public class FragmentCompute : NodeOfType<FragmentShaderComputeNode> {}
  public class TextureCompute : NodeOfType<TextureComputeNode> {}
  public class Read : NodeOfType<ReadNode> {}
  public class ReadTexture : NodeOfType<ReadTextureNode> {}
  public class Math : NodeOfType<MathNode> {}
  public class MakeArray : NodeOfType<MakeArrayNode>{}
  public class FillArray : NodeOfType<FillArrayNode>{}
  public class ReduceArray : NodeOfType<ReduceArrayNode>{}
  public class Concat : NodeOfType<ConcatNode>{}
  public class VectorIndex : NodeOfType<VectorIndexNode>{}
  public class Pack : NodeOfType<PackNode>{}
  public class Unpack : NodeOfType<UnpackNode>{}
  public class Switch : NodeOfType<SwitchNode>{}
  public class Literal : NodeOfType<LiteralNode>{}
  public class GenerateValue : NodeOfType<GenerateValueNode>{}
  public class Latch : NodeOfType<LatchNode>{}
  public class ValueIn : NodeOfType<ValueInputNode>{}
  public class TextureIn : NodeOfType<TextureInputNode>{}

  public class TypeDecl : NodeOfType<TypeDeclNode>{}
}
