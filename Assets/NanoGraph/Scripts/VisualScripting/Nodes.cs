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
                Func<DValue> valueProvider = () => defaultValues.GetValueOrDefault(inputPort.key) as DValue? ?? default;
                literalNode.InternalValueProvider = valueProvider;
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
  [Alias(typeof(MathAliasProvider))]
  public class Math : NodeOfType<MathNode> {}
  public class MakeArray : NodeOfType<MakeArrayNode>{}
  [Alias(typeof(FillArrayAliasProvider))]
  public class FillArray : NodeOfType<FillArrayNode>{}
  [Alias(typeof(ReduceArrayAliasProvider))]
  public class ReduceArray : NodeOfType<ReduceArrayNode>{}
  public class Concat : NodeOfType<ConcatNode>{}
  [Alias(typeof(VectorIndexAliasProvider))]
  public class VectorIndex : NodeOfType<VectorIndexNode>{}
  [Alias(typeof(PackAliasProvider))]
  public class Pack : NodeOfType<PackNode>{}
  public class Unpack : NodeOfType<UnpackNode>{}
  public class Switch : NodeOfType<SwitchNode>{}
  [Alias(typeof(LiteralAliasProvider))]
  public class Literal : NodeOfType<LiteralNode>{}
  [Alias(typeof(GenerateValueAliasProvider))]
  public class GenerateValue : NodeOfType<GenerateValueNode>{}
  public class Latch : NodeOfType<LatchNode>{}
  [Alias(typeof(ValueInAliasProvider))]
  public class ValueIn : NodeOfType<ValueInputNode>{}
  public class TextureIn : NodeOfType<TextureInputNode>{}

  public class TypeDecl : NodeOfType<TypeDeclNode>{}

  public class MathAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(StandardOperatorType));
      StandardOperatorType[] values = Enum.GetValues(typeof(StandardOperatorType)).Cast<StandardOperatorType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = names[i];
        StandardOperatorType value = values[i];
        yield return (name, null, unit => ((unit as BaseNode).Node as MathNode).Operator = value);
      }
    }
  }

  public class FillArrayAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(ValueProviderType));
      ValueProviderType[] values = Enum.GetValues(typeof(ValueProviderType)).Cast<ValueProviderType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"Fill{names[i]}";
        ValueProviderType value = values[i];
        yield return (name, null, unit => ((unit as BaseNode).Node as FillArrayNode).Source = value);
      }
    }
  }

  public class ReduceArrayAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(ReduceOperatorType));
      ReduceOperatorType[] values = Enum.GetValues(typeof(ReduceOperatorType)).Cast<ReduceOperatorType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"Array{names[i]}";
        ReduceOperatorType value = values[i];
        yield return (name, null, unit => ((unit as BaseNode).Node as ReduceArrayNode).Operation = value);
      }
    }
  }

  public class GenerateValueAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(ValueProviderType));
      ValueProviderType[] values = Enum.GetValues(typeof(ValueProviderType)).Cast<ValueProviderType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"Generate{names[i]}";
        ValueProviderType value = values[i];
        if (value == ValueProviderType.Value) {
          continue;
        }
        yield return (name, null, unit => ((unit as BaseNode).Node as GenerateValueNode).Source = value);
      }
    }
  }

  public class PackAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(PackableType));
      PackableType[] values = Enum.GetValues(typeof(PackableType)).Cast<PackableType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"Pack{names[i]}";
        PackableType value = values[i];
        if (value == PackableType.Custom) {
          continue;
        }
        yield return (name, null, unit => ((unit as BaseNode).Node as PackNode).Type = value);
      }
    }
  }

  public class LiteralAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(PrimitiveType));
      PrimitiveType[] values = Enum.GetValues(typeof(PrimitiveType)).Cast<PrimitiveType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"{names[i]}Literal";
        PrimitiveType value = values[i];
        if (value == PrimitiveType.Texture ||
            value == PrimitiveType.TypeDecl ||
            value == PrimitiveType.Vertices) {
          continue;
        }
        yield return (name, null, unit => ((unit as BaseNode).Node as LiteralNode).Type = value);
      }
    }
  }

  public class ValueInAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(ValueInputNode.InputType));
      ValueInputNode.InputType[] values = Enum.GetValues(typeof(ValueInputNode.InputType)).Cast<ValueInputNode.InputType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"{names[i]}In";
        ValueInputNode.InputType value = values[i];
        yield return (name, null, unit => ((unit as BaseNode).Node as ValueInputNode).Type = value);
      }
    }
  }

  public class VectorIndexAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(VectorIndexNode.DimensionType));
      VectorIndexNode.DimensionType[] values = Enum.GetValues(typeof(VectorIndexNode.DimensionType)).Cast<VectorIndexNode.DimensionType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"{names[i]}Index";
        VectorIndexNode.DimensionType value = values[i];
        yield return (name, null, unit => ((unit as BaseNode).Node as VectorIndexNode).Dimensions = value);
      }
    }
  }
}
