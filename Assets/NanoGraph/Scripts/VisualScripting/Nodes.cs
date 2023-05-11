using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;
using Unity.VisualScripting;

namespace NanoGraph.VisualScripting {
  public abstract class BaseNode : Unit, IDCustomInspectorDataProvider, IValueEditedHandler {
    public abstract NanoGraph Graph { get; }
    public abstract string DebugId { get; }
    public abstract DataSpec NodeInputSpec { get; }
    public abstract DataSpec NodeOutputSpec { get; }
    public abstract IEditableAttributeProvider EditableAttributeProvider { get; }
    protected abstract DataEdge[] GetAllInputDataEdges();
    protected abstract IDataNode GetNodeConnectedToInputPort(string fieldName, out string actualFieldName);
    public bool DebugEnabled = true;

    private readonly Dictionary<string, IUnitInputPort> _fieldNameToInputPortMap = new Dictionary<string, IUnitInputPort>();

    [Serialize]
    private readonly Dictionary<string, LiteralNode> _fieldNameToLiteralNodes = new Dictionary<string, LiteralNode>();
    [NonSerialized]
    private bool _didConnectSerializedLiteralNodes = false;
    private readonly Dictionary<string, DCustomInspectorData> _fieldNameToCustomInspectorData = new Dictionary<string, DCustomInspectorData>();

    protected readonly Action _nodeInvalidatedHandler;

    public BaseNode() {
      _nodeInvalidatedHandler = () => { PortsChanged(); Define(); SyncInputConnectionsToGraphEdges(); };
    }

    public override void BeforeAdd() {
      base.BeforeAdd();
      EnsureNode();
    }

    public override void AfterAdd() {
      base.AfterAdd();
      SyncInputConnectionsToGraphEdges();
    }

    public override void AfterRemove() {
      base.AfterRemove();
      foreach (LiteralNode node in _fieldNameToLiteralNodes.Values) {
        node?.Graph?.RemoveNode(node);
      }
      _fieldNameToLiteralNodes.Clear();
    }

    public virtual void NotifyInputConnectionsChanged() {
      SyncInputConnectionsToGraphEdges();
    }

    public virtual void NotifyOutputConnectionsChanged() {
      SyncOutputNodeRegistration();
    }

    void IValueEditedHandler.OnValueEdited(string fieldName) {
      if (_fieldNameToLiteralNodes.TryGetValue(fieldName, out LiteralNode literalNode)) {
        Plugin.PluginService.Instance.SetDebugValue(literalNode.DebugValueKey, literalNode.EffectiveInternalValue.ValueArray);
        return;
      }
      Graph?.CompileLater();
    }

    protected override void Definition() {
      EnsureNode();
      _fieldNameToCustomInspectorData.Clear();
      DataSpec inputSpec = NodeInputSpec;
      DataSpec outputSpec = NodeOutputSpec;
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
            case PrimitiveType.BufferRef:
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
      foreach (LiteralNode literalNode in _fieldNameToLiteralNodes.Values) {
        literalNode.DebugEnabled = DebugEnabled;
      }
    }

    public DCustomInspectorData? ProvideCustomInspectorData(string key) {
      if (_fieldNameToCustomInspectorData.TryGetValue(key, out var data)) {
        return data;
      }
      return null;
    }

    protected abstract void EnsureNode();

    public virtual void SyncInputConnectionsToGraphEdges() {
      // Debug.Log($"Syncing Input: {this}");
      NanoGraph graph = Graph;
      DataEdge[] oldInputEdges = GetAllInputDataEdges();

      DataSpec inputSpec = NodeInputSpec;
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
        IDataNode newSourceNode = GetSourceNodeOrNull(inputConnection.source.unit, inputConnection.source.key, out string actualKey);
        if (oldSourceNode != newSourceNode || actualKey != oldInputEdge.Source.FieldName) {
          edgesToRemove.Add(oldInputEdge);
          edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = newSourceNode, FieldName = actualKey }, Destination = oldInputEdge.Destination });
          continue;
        }
      }
      // Look for new connections.
      foreach (IUnitInputPort inputPort in inputs) {
        IDataNode outputNode = GetNodeConnectedToInputPort(inputPort.key, out string actualInputPortKey);
        var inputConnection = inputPort.connections.FirstOrDefault();
        IDataNode inputNode = GetSourceNodeOrNull(inputConnection?.source.unit, inputConnection?.source.key, out string actualKey);
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
                literalNode.DebugEnabled = DebugEnabled;
                graph.AddNode(literalNode);
                edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = literalNode, FieldName = "Out" }, Destination = new DataPlug { Node = outputNode, FieldName = actualInputPortKey } });
              }
            }
          } else {
            relinkNode = true;
          }
          if (relinkNode) {
            bool isArray = field?.Type.IsArray ?? false;
            Func<DValue> valueProvider = () => defaultValues.GetValueOrDefault(inputPort.key) as DValue? ?? default;
            literalNode = new LiteralNode { Name = "Value", Type = fieldPrimitiveType, ValueSource = InputSource.Internal, InternalValueProvider = valueProvider, DebugEnabled = DebugEnabled };
            _fieldNameToLiteralNodes[inputPort.key] = literalNode;

            graph.AddNode(literalNode);
            edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = literalNode, FieldName = "Out" }, Destination = new DataPlug { Node = outputNode, FieldName = actualInputPortKey } });
          }
          continue;
        }
        // Input is connected. Check if it is already mapped.
        if (oldInputEdges.Any(edge => edge.Destination.FieldName == inputPort.key)) {
          continue;
        }
        // Input is connected and not in the existing edges. Add it.
        edgesToAdd.Add(new DataEdge { Source = new DataPlug { Node = inputNode, FieldName = actualKey }, Destination = new DataPlug { Node = outputNode, FieldName = actualInputPortKey } });
      }

      foreach (var entry in _fieldNameToLiteralNodes.ToArray()) {
        if (!_fieldNameToInputPortMap.TryGetValue(entry.Key, out IUnitInputPort inputPort)) {
          RemoveLiteralNode(entry);
          continue;
        }
        bool isConnected = inputPort.connections.Any(connection => GetSourceNodeOrNull(connection.source.unit, connection.source.key, out _) != null);
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

    public void RegenerateInternalNodeDebugIds() {
      foreach (var entry in _fieldNameToLiteralNodes) {
        entry.Value.RegenerateDebugId();
      }
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

    protected virtual void SyncOutputNodeRegistration() {}

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

    // public static BaseNode GetSourceBaseNodeOrNull(IUnit src, string key, out string actualKey) {
    public static IDataNode GetSourceNodeOrNull(IUnit src, string key, out string actualKey) {
      actualKey = key;
      if (src is SubgraphUnit subgraphUnit) {
        GraphOutput graphOutput = subgraphUnit?.nest?.graph?.units?.FirstOrDefault(unit => unit is GraphOutput) as GraphOutput;
        if (graphOutput == null) {
          return null;
        }
        foreach (var connection in graphOutput.connections) {
          if (connection.destination.key == key) {
            if (connection.source.unit is NodeBasedNode baseNode) {
              return baseNode.Node;
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
            return GetSourceNodeOrNull(connection.source.unit, connection.source.key, out actualKey);
          }
        }
      } else if (src is EmbeddedNode embeddedNode) {
        return embeddedNode.GetNodeConnectedToOutputPort(key, out actualKey);
      } else if (src is NodeBasedNode baseNode) {
        return baseNode.Node;
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
      bool hadEntry = false;
      bool sweepNulls = false;
      foreach ((WeakReference graphRef, SubgraphUnit unitEntry) in _weakNestedGraphs) {
        if (graphRef.Target is FlowGraph graphEntry) {
          if (graphEntry == graph) {
            hadEntry = true;
            break;
          }
        } else {
          sweepNulls = true;
        }
      }
      if (!hadEntry) {
        _weakNestedGraphs.Add((new WeakReference(graph), subgraphUnit));
        foreach (IUnit unit in graph.units) {
          if (unit is SubgraphUnit child) {
            MapSubgraphUnit(child);
          } else if (unit is BaseNode baseNode) {
            baseNode.EnsureNode();
            baseNode.SyncInputConnectionsToGraphEdges();
          }
        }
      }
      if (sweepNulls) {
        _weakNestedGraphs.RemoveAll(entry => !entry.Item1.IsAlive);
      }
    }
  }

  public class EmbeddedNode : BaseNode {
    [Serialize]
    public ScriptGraphAsset ScriptGraphAssetReference;

    public FlowGraph FlowGraph => _flowGraph;

    [DoNotSerialize]
    private NanoGraph _graph;
    public override NanoGraph Graph => _graph;
    public override string DebugId => null;
    public override IEditableAttributeProvider EditableAttributeProvider => null;

    [DoNotSerialize]
    private FlowGraph _flowGraph;
    [Serialize]
    private DataSpec _inputSpec = DataSpec.Empty;
    [Serialize]
    private DataSpec _outputSpec = DataSpec.Empty;

    public override DataSpec NodeInputSpec => _inputSpec;
    public override DataSpec NodeOutputSpec => _outputSpec;

    [DoNotSerialize]
    private Dictionary<string, RouteNode> _inputRouteNodes = new Dictionary<string, RouteNode>();
    [DoNotSerialize]
    private Dictionary<string, RouteNode> _outputRouteNodes = new Dictionary<string, RouteNode>();

    protected override DataEdge[] GetAllInputDataEdges() => Array.Empty<DataEdge>();
    protected override IDataNode GetNodeConnectedToInputPort(string fieldName, out string actualFieldName) {
      if (_inputRouteNodes.TryGetValue(fieldName, out RouteNode routeNode)) {
        actualFieldName = "In";
        return routeNode;
      }
      actualFieldName = null;
      return null;
    }
    public IDataNode GetNodeConnectedToOutputPort(string fieldName, out string actualFieldName) {
      if (_outputRouteNodes.TryGetValue(fieldName, out RouteNode routeNode)) {
        actualFieldName = "Out";
        return routeNode;
      }
      actualFieldName = null;
      return null;
    }

    public override void BeforeAdd() {
      base.BeforeAdd();
    }

    public override void AfterAdd() {
      base.AfterAdd();
    }

    public override void AfterRemove() {
      base.AfterRemove();
      _flowGraph?.Dispose();
    }

    public override void Dispose() {
      base.Dispose();
      _flowGraph?.Dispose();
    }

    public override void NotifyInputConnectionsChanged() {
      base.NotifyInputConnectionsChanged();
    }

    public override void NotifyOutputConnectionsChanged() {
      base.NotifyOutputConnectionsChanged();
    }

    protected override void Definition() {
      base.Definition();
    }

    public override void SyncInputConnectionsToGraphEdges() {
      if (_flowGraph == null) {
        return;
      }
      NanoGraph graph = _graph;
      GraphInput graphInputUnit = _flowGraph.units.FirstOrDefault(unit => unit is GraphInput) as GraphInput;
      GraphOutput graphOutputUnit = _flowGraph.units.FirstOrDefault(unit => unit is GraphOutput) as GraphOutput;

      if (graphInputUnit != null) {
        var routeNodes = _inputRouteNodes;
        HashSet<string> removedKeys = new HashSet<string>(routeNodes.Keys);
        foreach (var port in graphInputUnit.valueOutputs) {
          string key = port.key;
          removedKeys.Remove(key);
          if (!routeNodes.TryGetValue(key, out RouteNode routeNode)) {
            routeNode = new RouteNode { TypeSpecSource = InputSource.External, DebugEnabled = DebugEnabled };
            graph.AddNode(routeNode);
            routeNodes[key] = routeNode;

            // foreach (var connection in port.connections) {
            //   foreach (var dest in BaseNode.GetDestBaseNodes(connection.destination.unit, connection.destination.key)) {
            //     // TODO: Fix.
            //     graph.Connect(routeNode, "Out", (dest as NodeBasedNode).Node, connection.destination.key);
            //   }
            // }
          }
          foreach (var connection in port.connections) {
            foreach (var dest in BaseNode.GetDestBaseNodes(connection.destination.unit, connection.destination.key)) {
              // TODO: Fix.
              graph.Connect(routeNode, "Out", (dest as NodeBasedNode).Node, connection.destination.key);
            }
          }
        }
        foreach (string removedKey in removedKeys) {
          RouteNode node = routeNodes[removedKey];
          routeNodes.Remove(removedKey);
          graph.RemoveNode(node);
        }
      }
      if (graphOutputUnit != null) {
        var routeNodes = _outputRouteNodes;
        HashSet<string> removedKeys = new HashSet<string>(routeNodes.Keys);
        foreach (var port in graphOutputUnit.valueInputs) {
          string key = port.key;
          removedKeys.Remove(key);
          if (!routeNodes.TryGetValue(key, out RouteNode routeNode)) {
            routeNode = new RouteNode { TypeSpecSource = InputSource.Internal, DebugEnabled = DebugEnabled };
            graph.AddNode(routeNode);
            routeNodes[key] = routeNode;
            var connection = port.connections.FirstOrDefault();
            if (connection != null) {
              IDataNode input = BaseNode.GetSourceNodeOrNull(connection.source.unit, connection.source.key, out string actualKey);
              if (input != null) {
                graph.Connect(input, actualKey, routeNode, "In");
              }
            }
          }
          {
            var connection = port.connections.FirstOrDefault();
            if (connection != null) {
              IDataNode input = BaseNode.GetSourceNodeOrNull(connection.source.unit, connection.source.key, out string actualKey);
              if (input != null) {
                DataField field = input.OutputSpec.Fields.First(field => field.Name == actualKey);
                routeNode.InternalTypeSpec = field.Type;
              }
            }
          }
        }
        foreach (string removedKey in removedKeys) {
          RouteNode node = routeNodes[removedKey];
          routeNodes.Remove(removedKey);
          graph.RemoveNode(node);
        }
      }

      base.SyncInputConnectionsToGraphEdges();
    }

    public void LoadFromScriptGraphAsset() {
      if (ScriptGraphAssetReference && ScriptGraphAssetReference.graph.units.Count > 0) {
        _flowGraph = Unity.VisualScripting.Serialization.CloneViaSerialization(ScriptGraphAssetReference.graph);
      }
      if (_flowGraph == null) {
        return;
      }
      _inputSpec = DataSpec.FromFields(_flowGraph.valueInputDefinitions.Select(port => DataField.MakePrimitive(port.key, PrimitiveType.Float)).ToArray());
      _outputSpec = DataSpec.FromFields(_flowGraph.valueOutputDefinitions.Select(port => DataField.MakePrimitive(port.key, PrimitiveType.Float)).ToArray());
      _flowGraph.Prewarm();
      foreach (var unit in _flowGraph.units) {
        BaseNode baseNode = unit as BaseNode;
        if (baseNode != null) {
          baseNode.DebugEnabled = false;
        }
        DataNode node = (unit as NodeBasedNode)?.Node as DataNode;
        if (node != null) {
          node.DebugEnabled = false;
        }
        unit.Define();
      }
      foreach (var unit in _flowGraph.units) {
        BaseNode baseNode = unit as BaseNode;
        if (baseNode == null) {
          continue;
        }
        baseNode.SyncInputConnectionsToGraphEdges();
      }
    }

    protected override void EnsureNode() {
      _graph = NanoGraph.DebugInstance;
      if (_flowGraph != null) { 
        return;
      }
      LoadFromScriptGraphAsset();
    }
  }

  public abstract class NodeBasedNode : BaseNode {
    [Serialize]
    public IDataNode Node;
    [Serialize]
    public string SerializedDebugId;
    public override NanoGraph Graph => Node?.Graph;
    public override string DebugId => Node?.DebugId;
    public override IEditableAttributeProvider EditableAttributeProvider => Node as IEditableAttributeProvider;

    public override DataSpec NodeInputSpec => Node.InputSpec;
    public override DataSpec NodeOutputSpec => Node.OutputSpec;

    private bool _isRegisteredAsOutputNode;

    private HashSet<string> _cachedResourceDeps = new HashSet<string>();
    private bool _resourceDepsListenerAdded = false;
    private Action<string> _resourceDepsListener;

    protected abstract IDataNode CreateNode();
    protected override DataEdge[] GetAllInputDataEdges() => Graph.GetInputEdges(Node);
    protected override IDataNode GetNodeConnectedToInputPort(string fieldName, out string actualFieldName) {
      actualFieldName = fieldName;
      return Node;
    }

    public override void BeforeAdd() {
      base.BeforeAdd();
      Node.Graph.AddNodeInvalidatedHandler(Node, _nodeInvalidatedHandler);
    }

    public override void AfterAdd() {
      base.AfterAdd();
    }

    public override void AfterRemove() {
      base.AfterRemove();
      // TODO: Remove node.
      Node?.Graph?.RemoveNode(Node);
      Node?.Graph?.RemoveNodeInvalidatedHandler(Node, _nodeInvalidatedHandler);
      EditorUtils.DelayCall += RemoveResourceDeps;
    }

    public override void Dispose() {
      base.Dispose();
      EditorUtils.DelayCall += RemoveResourceDeps;
    }

    public override void NotifyInputConnectionsChanged() {
      base.NotifyInputConnectionsChanged();
      (Node as IAutoTypeNode)?.UpdateTypesFromInputs();
    }

    public override void NotifyOutputConnectionsChanged() {
      base.NotifyOutputConnectionsChanged();
    }

    protected override void SyncOutputNodeRegistration() {
      base.SyncOutputNodeRegistration();

      bool isOutputNode = false;
      foreach (var output in outputs) {
        foreach (var connection in output.connections) {
          if (connection.destination.unit is GraphOutput graphOutput) {
            if (GetSubgraphUnit(graphOutput.graph) == null) {
              isOutputNode = true;
            }
            break;
          }
        }
      }
      if (isOutputNode) {
        if (NanoGraph.DebugScriptInstance != this.graph) {
          isOutputNode = false;
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

    protected override void Definition() {
      base.Definition();
      Node.DebugId = this.guid.ToString();

      if (SerializedDebugId != Node.DebugId) {
        SerializedDebugId = Node.DebugId;
        RegenerateInternalNodeDebugIds();
      }

      Node.Graph.MarkNodeDirty(Node);
      Node.Graph.ValidateLater();

      if (Node is IResourceDepsNode resourceDepsNode) {
        EditorUtils.DelayCall += AddResourceDeps;
      }
    }

    protected override void EnsureNode() {
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

    private void AddResourceDeps() {
      if (!(Node is IResourceDepsNode resourceDepsNode)) {
        return;
      }
      _cachedResourceDeps = new HashSet<string>(resourceDepsNode.DependentAssetPaths);
      if (_resourceDepsListenerAdded) {
        return;
      }
      _resourceDepsListenerAdded = true;

      _resourceDepsListener = path => {
        if (_cachedResourceDeps.Contains(path)) {
          NanoGraph.DebugInstance?.CompileLater();
        }
      };
      AssetChangeDetector.Instance.AssetPathChanged += _resourceDepsListener;
    }

    private void RemoveResourceDeps() {
      if (!_resourceDepsListenerAdded) {
        return;
      }
      _resourceDepsListenerAdded = false;
      if (_resourceDepsListener == null) {
        return;
      }
      AssetChangeDetector.Instance.AssetPathChanged -= _resourceDepsListener;
      _resourceDepsListener = null;
    }
  }

  public abstract class NodeOfType<T> : NodeBasedNode where T : IDataNode, new() {
    protected override IDataNode CreateNode() => new T();
  }



  public class VectorCompute : NodeOfType<VectorComputeNode> {}
  [Alias(typeof(ScalarComputeAliasProvider))]
  public class ScalarCompute : NodeOfType<ScalarComputeNode> {}
  public class VertexCompute : NodeOfType<VertexShaderComputeNode> {}
  public class FragmentCompute : NodeOfType<FragmentShaderComputeNode> {}
  public class TextureCompute : NodeOfType<TextureComputeNode> {}
  public class Read : NodeOfType<ReadNode> {}
  public class ReadTexture : NodeOfType<ReadTextureNode> {}
  [Alias(typeof(MathAliasProvider))]
  public class Math : NodeOfType<MathNode> {}
  [Alias(typeof(MixAliasProvider))]
  public class Mix : NodeOfType<MixNode> {}
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
  [Alias(typeof(RepackAliasProvider))]
  public class Repack : NodeOfType<RepackNode>{}
  public class Switch : NodeOfType<SwitchNode>{}
  [Alias(typeof(LiteralAliasProvider))]
  public class Literal : NodeOfType<LiteralNode>{}
  public class Route : NodeOfType<RouteNode>{}
  [Alias(typeof(GenerateValueAliasProvider))]
  public class GenerateValue : NodeOfType<GenerateValueNode>{}
  public class Latch : NodeOfType<LatchNode>{}
  public class ReadBufferRef : NodeOfType<ReadBufferRefNode>{}
  [Alias(typeof(ExpressionAliasProvider))]
  public class Expression : NodeOfType<ExpressionNode>{}
  [Alias(typeof(ValueInAliasProvider))]
  public class ValueIn : NodeOfType<ValueInputNode>{}
  public class TextureIn : NodeOfType<TextureInputNode>{}
  public class NioTextureIn : NodeOfType<NioTextureInputNode>{}
  public class PonkOutput : NodeOfType<PonkOutputNode>{}

  public class TypeDecl : NodeOfType<TypeDeclNode>{}

  public class ScalarComputeAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(BasicOutputType));
      BasicOutputType[] values = Enum.GetValues(typeof(BasicOutputType)).Cast<BasicOutputType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"{names[i]}ScalarCompute";
        BasicOutputType value = values[i];
        if (value == BasicOutputType.Custom ||
            value == BasicOutputType.Texture) {
          continue;
        }
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as ScalarComputeNode).OutputType = value);
      }
    }
  }

  public class MathAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(StandardOperatorType));
      StandardOperatorType[] values = Enum.GetValues(typeof(StandardOperatorType)).Cast<StandardOperatorType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = names[i];
        StandardOperatorType value = values[i];
        string[] aliases = null;
        if (value == StandardOperatorType.Subtract) {
          aliases = new[] { "Minus", "-" };
        } else if (value == StandardOperatorType.Negate) {
          aliases = new[] { "-" };
        } else if (value == StandardOperatorType.OneMinus) {
          aliases = new[] { "1 -" };
        } else if (value == StandardOperatorType.Invert) {
          aliases = new[] { "1 /" };
        }
        yield return (name, aliases, unit => ((unit as NodeBasedNode).Node as MathNode).Operator = value);
      }
    }
  }

  public class MixAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      string[] names = Enum.GetNames(typeof(StandardMixType));
      StandardMixType[] values = Enum.GetValues(typeof(StandardMixType)).Cast<StandardMixType>().ToArray();
      for (int i = 0; i < names.Length; ++i) {
        string name = $"{names[i]}Mix";
        StandardMixType value = values[i];
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as MixNode).MixType = value);
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
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as FillArrayNode).Source = value);
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
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as ReduceArrayNode).Operation = value);
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
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as GenerateValueNode).Source = value);
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
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as PackNode).Type = value);
      }
    }
  }

  public class RepackAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      void SetBinaryType(IUnit unit, PrimitiveType a, PrimitiveType b) {
        RepackNode node = ((unit as NodeBasedNode).Node as RepackNode);
        node.Fields = new TypeDeclBuilder { Fields = { TypeDeclBuilderField.Make("0", a), TypeDeclBuilderField.Make("1", b) } };
      }
      void SetTrinaryType(IUnit unit, PrimitiveType a, PrimitiveType b, PrimitiveType c) {
        RepackNode node = ((unit as NodeBasedNode).Node as RepackNode);
        node.Fields = new TypeDeclBuilder { Fields = { TypeDeclBuilderField.Make("0", a), TypeDeclBuilderField.Make("1", b), TypeDeclBuilderField.Make("2", c) } };
      }
      void SetOutputBinaryType(IUnit unit, PrimitiveType input, PrimitiveType a, PrimitiveType b) {
        RepackNode node = ((unit as NodeBasedNode).Node as RepackNode);
        node.AutoOutType = false;
        node.Fields = new TypeDeclBuilder { Fields = { TypeDeclBuilderField.Make("0", input) } };
        node.OutFields = new TypeDeclBuilder { Fields = { TypeDeclBuilderField.Make("0", a), TypeDeclBuilderField.Make("1", b) } };
      }
      void SetOutputTrinaryType(IUnit unit, PrimitiveType input, PrimitiveType a, PrimitiveType b, PrimitiveType c) {
        RepackNode node = ((unit as NodeBasedNode).Node as RepackNode);
        node.AutoOutType = false;
        node.Fields = new TypeDeclBuilder { Fields = { TypeDeclBuilderField.Make("0", input) } };
        node.OutFields = new TypeDeclBuilder { Fields = { TypeDeclBuilderField.Make("0", a), TypeDeclBuilderField.Make("1", b), TypeDeclBuilderField.Make("2", c) } };
      }
      yield return ("Repack 2-1", null, unit => SetBinaryType(unit, PrimitiveType.Float2, PrimitiveType.Float));
      yield return ("Repack 1-2", null, unit => SetBinaryType(unit, PrimitiveType.Float, PrimitiveType.Float2));
      yield return ("Repack 3-1", null, unit => SetBinaryType(unit, PrimitiveType.Float3, PrimitiveType.Float));
      yield return ("Repack 2-2", null, unit => SetBinaryType(unit, PrimitiveType.Float2, PrimitiveType.Float2));
      yield return ("Repack 1-3", null, unit => SetBinaryType(unit, PrimitiveType.Float, PrimitiveType.Float3));
      yield return ("Repack 2-1-1", null, unit => SetTrinaryType(unit, PrimitiveType.Float2, PrimitiveType.Float, PrimitiveType.Float));
      yield return ("Repack 1-2-1", null, unit => SetTrinaryType(unit, PrimitiveType.Float, PrimitiveType.Float2, PrimitiveType.Float));
      yield return ("Repack 1-1-2", null, unit => SetTrinaryType(unit, PrimitiveType.Float, PrimitiveType.Float, PrimitiveType.Float2));
      yield return ("Repack 3=>2-1", null, unit => SetOutputBinaryType(unit, PrimitiveType.Float3, PrimitiveType.Float2, PrimitiveType.Float));
      yield return ("Repack 3=>1-2", null, unit => SetOutputBinaryType(unit, PrimitiveType.Float3, PrimitiveType.Float, PrimitiveType.Float2));
      yield return ("Repack 3=>3-1", null, unit => SetOutputBinaryType(unit, PrimitiveType.Float3, PrimitiveType.Float3, PrimitiveType.Float));
      yield return ("Repack 4=>2-2", null, unit => SetOutputBinaryType(unit, PrimitiveType.Float4, PrimitiveType.Float2, PrimitiveType.Float2));
      yield return ("Repack 4=>1-3", null, unit => SetOutputBinaryType(unit, PrimitiveType.Float4, PrimitiveType.Float, PrimitiveType.Float3));
      yield return ("Repack 4=>2-1-1", null, unit => SetOutputTrinaryType(unit, PrimitiveType.Float4, PrimitiveType.Float2, PrimitiveType.Float, PrimitiveType.Float));
      yield return ("Repack 4=>1-2-1", null, unit => SetOutputTrinaryType(unit, PrimitiveType.Float4, PrimitiveType.Float, PrimitiveType.Float2, PrimitiveType.Float));
      yield return ("Repack 4=>1-1-2", null, unit => SetOutputTrinaryType(unit, PrimitiveType.Float4, PrimitiveType.Float, PrimitiveType.Float, PrimitiveType.Float2));
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
            value == PrimitiveType.Auto ||
            value == PrimitiveType.TypeDecl ||
            value == PrimitiveType.Vertices ||
            value == PrimitiveType.BufferRef) {
          continue;
        }
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as LiteralNode).Type = value);
      }
    }
  }

  public class ExpressionAliasProvider : IAliasProvider {
    public IEnumerable<string> GetAliases() => null;
    public IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives() {
      {
        void Configure(ExpressionNode node) {
          node.InputFields.Fields.Add(TypeDeclBuilderField.Make("x", PrimitiveType.Float));
          node.OutputFields.Fields.Add(TypeDeclBuilderField.Make("out", PrimitiveType.Float));
        }
        yield return ("UnaryExpression", null, unit => Configure((unit as NodeBasedNode).Node as ExpressionNode));
      }
      {
        void Configure(ExpressionNode node) {
          node.InputFields.Fields.Add(TypeDeclBuilderField.Make("x", PrimitiveType.Float));
          node.InputFields.Fields.Add(TypeDeclBuilderField.Make("y", PrimitiveType.Float));
          node.OutputFields.Fields.Add(TypeDeclBuilderField.Make("out", PrimitiveType.Float));
        }
        yield return ("BinaryExpression", null, unit => Configure((unit as NodeBasedNode).Node as ExpressionNode));
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
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as ValueInputNode).Type = value);
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
        yield return (name, null, unit => ((unit as NodeBasedNode).Node as VectorIndexNode).Dimensions = value);
      }
    }
  }
}
