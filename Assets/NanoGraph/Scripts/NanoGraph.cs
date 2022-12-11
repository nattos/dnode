using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class NanoGraph {
    public string EffectName = "Program";
    private readonly List<IDataNode> _nodes = new List<IDataNode>();
    private readonly HashSet<IDataNode> _dirtyNodes = new HashSet<IDataNode>();

    private readonly List<DataEdge> _edges = new List<DataEdge>();
    private readonly Dictionary<IDataNode, List<DataEdge>> _inputEdgesForNode = new Dictionary<IDataNode, List<DataEdge>>();
    private readonly Dictionary<IDataNode, List<DataEdge>> _outputEdgesForNode = new Dictionary<IDataNode, List<DataEdge>>();
    private readonly Dictionary<DataPlug, List<DataEdge>> _edgesBySource = new Dictionary<DataPlug, List<DataEdge>>();
    private readonly Dictionary<DataPlug, DataEdge> _edgesByDest = new Dictionary<DataPlug, DataEdge>();

    private readonly Dictionary<IDataNode, List<Action>> _nodeInvalidatedHanders = new Dictionary<IDataNode, List<Action>>();

    public void AddNode(IDataNode node) {
      Debug.Log($"Add Node: {node}");
      node.Graph?.RemoveNode(node);
      node.Graph = this;
      node.CacheData = new DataNodeCacheData();
      _nodes.Add(node);
      MarkNodeDirty(node);
      ValidateLater();
    }

    public void RemoveNode(IDataNode node) {
      if (node.Graph != this) {
        return;
      }
      Debug.Log($"Remove Node: {node}");
      node.Graph = null;
      DataEdge[] inputEdges = GetInputEdges(node);
      DataEdge[] outputEdges = GetInputEdges(node);
      foreach (var edge in inputEdges.Concat(outputEdges)) {
        Disconnect(edge.Source.Node, edge.Source.FieldName, edge.Destination.Node, edge.Destination.FieldName);
      }
      _inputEdgesForNode.Remove(node);
      _outputEdgesForNode.Remove(node);
      _nodes.Remove(node);
      _dirtyNodes.Remove(node);

      ValidateLater();
    }

    public void Connect(IDataNode sourceNode, string sourcePlugName, IDataNode destNode, string destPlugName) {
      var sourceSpec = sourceNode.OutputSpec;
      var destSpec = destNode.InputSpec;

      var sourceField = sourceSpec.Fields.FirstOrNull(field => field.Name == sourcePlugName);
      var destField = destSpec.Fields.FirstOrNull(field => field.Name == destPlugName);
      if (sourceField == null || destField == null) {
        return;
      }
      DataPlug sourcePlug = new DataPlug { Node = sourceNode, FieldName = sourcePlugName };
      DataPlug destPlug = new DataPlug { Node = destNode, FieldName = destPlugName };

      // Note: Allow multiple output connections.
      // RemoveEdgeForPlug(sourcePlug, _edgesBySource, _outputEdgesForNode);
      RemoveEdgeForPlug(destPlug, _edgesByDest, _inputEdgesForNode);

      DataEdge edge = new DataEdge { Source = sourcePlug, Destination = destPlug };
      _edges.Add(edge);
      AddEdgeForPlug(sourcePlug, edge, _edgesBySource, _outputEdgesForNode);
      AddEdgeForPlug(destPlug, edge, _edgesByDest, _inputEdgesForNode);

      MarkNodeDirty(sourceNode);
      MarkNodeDirty(destNode);

      Debug.Log($"Connected {sourcePlug} => {destPlug}");
      ValidateLater();
    }

    public void Disconnect(IDataNode sourceNode, string sourcePlugName, IDataNode destNode, string destPlugName) {
      if (!_edgesByDest.TryGetValue(new DataPlug { Node = destNode, FieldName = destPlugName }, out DataEdge edge)) {
        return;
      }

      RemoveEdgeForPlug(edge.Source, edge, _edgesBySource, _outputEdgesForNode);
      RemoveEdgeForPlug(edge.Destination, _edgesByDest, _inputEdgesForNode);
      _edges.Remove(edge);

      MarkNodeDirty(edge.Source.Node);
      MarkNodeDirty(edge.Destination.Node);

      Debug.Log($"Disconnected {edge.Source} => {edge.Destination}");
      ValidateLater();
    }

    private void RemoveEdgeForPlug(DataPlug plug, Dictionary<DataPlug, DataEdge> edgeMap, Dictionary<IDataNode, List<DataEdge>> nodeEdgeMap) {
      if (edgeMap.TryGetRemove(plug, out DataEdge edge)) {
        _edges.Remove(edge);
      }
      if (nodeEdgeMap.TryGetValue(plug.Node, out List<DataEdge> nodeEdges)) {
        nodeEdges.Remove(edge);
      }
    }

    private void RemoveEdgeForPlug(DataPlug plug, DataEdge edge, Dictionary<DataPlug, List<DataEdge>> edgeMap, Dictionary<IDataNode, List<DataEdge>> nodeEdgeMap) {
      if (edgeMap.TryGetValue(plug, out List<DataEdge> edges)) {
        edges.Remove(edge);
        if (edges.Count == 0) {
          edgeMap.Remove(plug);
        }
      }
      _edges.Remove(edge);
      if (nodeEdgeMap.TryGetValue(plug.Node, out List<DataEdge> nodeEdges)) {
        nodeEdges.Remove(edge);
      }
    }

    private void AddEdgeForPlug(DataPlug plug, DataEdge edge, Dictionary<DataPlug, DataEdge> edgeMap, Dictionary<IDataNode, List<DataEdge>> nodeEdgeMap) {
      edgeMap[plug] = edge;
      if (!nodeEdgeMap.TryGetValue(plug.Node, out List<DataEdge> nodeEdges)) {
        nodeEdges = new List<DataEdge>();
        nodeEdgeMap[plug.Node] = nodeEdges;
      }
      nodeEdges.Add(edge);
    }

    private void AddEdgeForPlug(DataPlug plug, DataEdge edge, Dictionary<DataPlug, List<DataEdge>> edgeMap, Dictionary<IDataNode, List<DataEdge>> nodeEdgeMap) {
      if (!edgeMap.TryGetValue(plug, out List<DataEdge> existingEdges)) {
        existingEdges = new List<DataEdge>();
        edgeMap[plug] = existingEdges;
      }
      existingEdges.Add(edge);
      if (!nodeEdgeMap.TryGetValue(plug.Node, out List<DataEdge> nodeEdges)) {
        nodeEdges = new List<DataEdge>();
        nodeEdgeMap[plug.Node] = nodeEdges;
      }
      nodeEdges.Add(edge);
    }

    public DataEdge GetEdgeToDestinationOrNull(IDataNode node, string fieldName) {
      return GetEdgeToDestinationOrNull(new DataPlug { Node = node, FieldName = fieldName });
    }

    public DataEdge GetEdgeToDestinationOrNull(DataPlug plug) {
      return _edgesByDest.GetOrDefault(plug);
    }

    public DataEdge[] GetInputEdges(IDataNode node) {
      return _inputEdgesForNode.GetOrDefault(node)?.ToArray() ?? Array.Empty<DataEdge>();
    }

    public DataEdge[] GetOutputEdges(IDataNode node) {
      return _outputEdgesForNode.GetOrDefault(node)?.ToArray() ?? Array.Empty<DataEdge>();
    }

    public void AddNodeInvalidatedHandler(IDataNode node, Action callback) {
      if (!_nodeInvalidatedHanders.TryGetValue(node, out List<Action> actions)) {
        actions = new List<Action>();
        _nodeInvalidatedHanders[node] = actions;
      }
      actions.Add(callback);
    }

    public void RemoveNodeInvalidatedHandler(IDataNode node, Action callback) {
      if (!_nodeInvalidatedHanders.TryGetValue(node, out List<Action> actions)) {
        return;
      }
      actions.Remove(callback);
      if (actions.Count == 0) {
        _nodeInvalidatedHanders.Remove(node);
      }
    }

    private bool _isValidateLaterInFlight = false;
    public void ValidateLater() {
      if (_isValidateLaterInFlight) {
        return;
      }
      _isValidateLaterInFlight = true;
      EditorUtils.InvokeLater(() => {
        _isValidateLaterInFlight = false;
        Validate();
      });
    }

    public void MarkNodeDirty(IDataNode node) {
      _dirtyNodes.Add(node);
    }

    public void Validate() {
      try {
        ValidateInner();
      } catch (Exception e) {
        Debug.LogException(e);
        _dirtyNodes.Clear();
      }
    }

    private void ValidateInner() {
      List<Action> cleanupActions = new List<Action>();

      _dirtyNodes.RemoveWhere(node => node.Graph != this);
      Queue<IDataNode> toVisit = new Queue<IDataNode>(_dirtyNodes);
      HashSet<IDataNode> alreadyQueued = new HashSet<IDataNode>(_dirtyNodes);
      void TryEnqueue(IDataNode node) {
        if (alreadyQueued.Add(node)) {
          toVisit.Enqueue(node);
        }
      }

      void AddDeleteEdgeCleanup(DataEdge edge) {
        cleanupActions.Add(() => {
          // TODO: Delete edge.
        });
      }

      void ValidateFieldsMatch(DataEdge edge, DataField? sourceField, DataField? destField) {
        if (destField == null && sourceField == null) {
          return;
        }
        if (destField == null || sourceField == null) {
          AddDeleteEdgeCleanup(edge);
          return;
        }
        if (!TypeSpec.IsConvertible(sourceField.Value.Type, destField.Value.Type)) {
          AddDeleteEdgeCleanup(edge);
          return;
        }
      }

      HashSet<IDataNode> invalidatedNodes = new HashSet<IDataNode>();
      while (toVisit.TryDequeue(out IDataNode node)) {
        // TODO: Probably not the right place to do this?
        (node as IAutoTypeNode)?.UpdateTypesFromInputs();
        var inputSpec = node.InputSpec;
        var outputSpec = node.OutputSpec;
        var cacheData = node.CacheData;

        // TODO: Run custom validation.
        node.Validate(cleanupActions);

        // Check if input spec changed.
        if (!FieldsEqual(cacheData.InputSpec, inputSpec.Fields)) {
          invalidatedNodes.Add(node);
          cacheData.InputSpec = inputSpec.Fields.ToArray();
          // Invalidate all input connections.
          foreach (var field in inputSpec.Fields) {
            var key = new DataPlug { Node = node, FieldName = field.Name };
            if (_edgesByDest.TryGetValue(key, out DataEdge edge)) {
              TryEnqueue(edge.Source.Node);
            }
          }
          // TODO: Be more precise. Only update the fields that actually changed.
        }
        // Check if output spec changed.
        if (!FieldsEqual(cacheData.OutputSpec, outputSpec.Fields)) {
          invalidatedNodes.Add(node);
          cacheData.OutputSpec = outputSpec.Fields.ToArray();
          // Invalidate all output connections.
          foreach (var field in outputSpec.Fields) {
            var key = new DataPlug { Node = node, FieldName = field.Name };
            if (_edgesBySource.TryGetValue(key, out List<DataEdge> edges)) {
              foreach (var edge in edges) {
                TryEnqueue(edge.Destination.Node);
              }
            }
          }
          // TODO: Be more precise. Only update the fields that actually changed.
        }
        // Check all input edge data types match.
        foreach (var field in inputSpec.Fields) {
          var key = new DataPlug { Node = node, FieldName = field.Name };
          if (_edgesByDest.TryGetValue(key, out DataEdge edge)) {
            ValidateFieldsMatch(edge, edge.SourceFieldOrNull, field);
          }
        }
        // Check all output edge data types match.
        foreach (var field in outputSpec.Fields) {
          var key = new DataPlug { Node = node, FieldName = field.Name };
          if (_edgesBySource.TryGetValue(key, out List<DataEdge> edges)) {
            foreach (var edge in edges) {
              ValidateFieldsMatch(edge, field, edge.DestinationFieldOrNull);
            }
          }
        }
        // TODO: Scrub dangling edges by source and dest.
      }
      // TODO: Scrub dangling nodes.

      _dirtyNodes.Clear();
      foreach (Action action in cleanupActions) {
        action.Invoke();
      }
      foreach (IDataNode invalidateNode in invalidatedNodes) {
        if (!_nodeInvalidatedHanders.TryGetValue(invalidateNode, out List<Action> actions)) {
          continue;
        }
        foreach (Action action in actions) {
          action.Invoke();
        }
      }
      if (_dirtyNodes.Count > 0) {
        // Some additional nodes were marked dirty while cleaning up.
        Validate();
      }
    }

    private static bool FieldsEqual(IReadOnlyList<DataField> lhs, IReadOnlyList<DataField> rhs) {
      if (lhs == rhs) {
        return true;
      }
      if (lhs == null || rhs == null) {
        return false;
      }
      if (lhs.Count != rhs.Count) {
        return false;
      }
      for (int i = 0; i < lhs.Count; ++i) {
        DataField lhsField = lhs[i];
        DataField rhsField = rhs[i];
        if (lhsField.Name != rhsField.Name ||
            lhsField.IsCompileTimeOnly != rhsField.IsCompileTimeOnly) {
          return false;
        }
        if (!StructuralComparisons.StructuralEqualityComparer.Equals(lhsField.Attributes ?? Array.Empty<string>(), rhsField.Attributes ?? Array.Empty<string>())) {
          return false;
        }
        if (!TypeSpecsEqual(lhsField.Type, rhsField.Type)) {
          return false;
        }
      }
      return true;
    }

    private static bool TypeSpecsEqual(TypeSpec lhs, TypeSpec rhs) {
      if (lhs.IsArray != rhs.IsArray ||
          lhs.Primitive != rhs.Primitive) {
        return false;
      }
      if (lhs.Type == null || rhs.Type == null) {
        return lhs.Type == null && rhs.Type == null;
      }
      return TypeFieldsEqual(lhs.Type.Fields, rhs.Type.Fields);
    }

    private static bool TypeFieldsEqual(IReadOnlyList<TypeField> lhs, IReadOnlyList<TypeField> rhs) {
      if (lhs == rhs) {
        return true;
      }
      if (lhs == null || rhs == null) {
        return false;
      }
      if (lhs.Count != rhs.Count) {
        return false;
      }
      for (int i = 0; i < lhs.Count; ++i) {
        TypeField lhsField = lhs[i];
        TypeField rhsField = rhs[i];
        if (lhsField.Name != rhsField.Name) {
          return false;
        }
        if (!StructuralComparisons.StructuralEqualityComparer.Equals(lhsField.Attributes ?? Array.Empty<string>(), rhsField.Attributes ?? Array.Empty<string>())) {
          return false;
        }
        if (!TypeSpecsEqual(lhsField.Type, rhsField.Type)) {
          return false;
        }
      }
      return true;
    }

    private interface ICodeGenerator {
      IDataNode SourceNode { get; }
      DataSpec InputSpec { get; }
      DataSpec OutputSpec { get; }
      void EmitCode(CodeContext context);
    }

    private class CodeGeneratorFromCodeNode : ICodeGenerator {
      public readonly ICodeNode Node;

      public CodeGeneratorFromCodeNode(ICodeNode node) {
        Node = node;
      }

      public IDataNode SourceNode => Node;
      public DataSpec InputSpec => Node.InputSpec;
      public DataSpec OutputSpec => Node.OutputSpec;
      public void EmitCode(CodeContext context) {
        Node.EmitCode(context);
      }
    }

    private class CodeGeneratorFromComputeNode : ICodeGenerator {
      public readonly IComputeNode Node;
      public readonly IComputeNode Root;
      public CodeCachedResult? InputResult;
      public IComputeNodeEmitCodeOperation Operation;

      public CodeGeneratorFromComputeNode(IComputeNode node, IComputeNode root) {
        Node = node;
        Root = root;
      }

      public void SetInput(CodeCachedResult inputResult, IComputeNodeEmitCodeOperation operation) {
        InputResult = inputResult;
        Operation = operation;
      }

      public IDataNode SourceNode => Node;
      public DataSpec InputSpec => DataSpec.Empty;
      public DataSpec OutputSpec => DataSpec.FromFields(Operation.GetInputsForDescendantNode(Root));

      public void EmitCode(CodeContext context) {
        if (InputResult == null) {
          context.Errors.Add($"Dependent node {Node} has not been defined yet.");
          return;
        }
        Operation.EmitLoadInputsForDescendantNode(Root, context);
      }
    }

    private List<ICodeGenerator> GenerateComputePlan(IComputeNode root, List<CodeGeneratorFromComputeNode> dependentComputeNodes, List<DataPlug> dependentComputeInputs, List<string> errors) {
      var computeNodeOrder = new System.Collections.Specialized.OrderedDictionary();
      // List<ICodeGenerator> computePlan = new List<ICodeGenerator>();
      Queue<IDataNode> queue = new Queue<IDataNode>();
      HashSet<IDataNode> wasQueued = new HashSet<IDataNode>();
      HashSet<DataPlug> dependentComputeInputsSet = new HashSet<DataPlug>();

      void QueueNode(DataPlug source) {
        IDataNode node = source.Node;
        if (node is IComputeNode) {
          // Hacky to do this here...
          if (dependentComputeInputsSet.Add(source)) {
            dependentComputeInputs.Add(source);
          }
        }
        if (computeNodeOrder.Contains(node)) {
          // TODO: Make this more efficient.
          // Boost the node back up in the order.
          computeNodeOrder.Remove(node);
          computeNodeOrder.Add(node, null);
          return;
        }
        computeNodeOrder.Add(node, null);
        queue.Enqueue(node);
      }

      foreach (var field in root.InputSpec.Fields) {
        var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = root, FieldName = field.Name });
        if (edge?.Source.Node == null) {
          continue;
        }
        QueueNode(edge.Source);
      }

      while (queue.TryDequeue(out IDataNode node)) {
        if (node is ICodeNode || node is ISplitComputeNode) {
          foreach (var field in node.InputSpec.Fields) {
            var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = node, FieldName = field.Name });
            if (edge?.Source.Node == null) {
              continue;
            }
            QueueNode(edge.Source);
          }
        } else if (node is IComputeNode) {
        } else if (node is ICompileTimeOnlyNode) {
        } else {
          errors.Add($"Node {node} is not an ICodeNode.");
        }
      }

      return computeNodeOrder.Keys.Cast<IDataNode>().Reverse().Select<IDataNode, ICodeGenerator>(node => {
        if (node is ICodeNode codeNode) {
          return new CodeGeneratorFromCodeNode(codeNode);
        } else if (node is IComputeNode computeNode) {
          var codeGenerator = new CodeGeneratorFromComputeNode(computeNode, root);
          dependentComputeNodes.Add(codeGenerator);
          return codeGenerator;
        } else if (node is ICompileTimeOnlyNode) {
          return null;
        } else {
          errors.Add($"Node {node} is not an ICodeNode.");
          return null;
        }
      }).Where(generator => generator != null).ToList();
    }

    public void GenerateProgram(IReadOnlyList<IComputeNode> outputNodes) {
      // TODO: Generate plan and scopes.
      // TODO: Deal with conditional scopes.
      // TODO: Deal with nodes in multiple compute contexts.
      List<string> errors = new List<string>();

      var program = new NanoProgram(EffectName);
      var executeFunction = program.AddOverrideFunction("Execute", NanoProgram.CpuContext, program.VoidType);
      var createPipelinesFunction = program.AddOverrideFunction("CreatePipelines", NanoProgram.CpuContext, program.VoidType);
      string objCPlaceholderClassIdentifier = $"{program.Identifier}_Placeholder";
      createPipelinesFunction.AddStatement($"NSError* error;");
      createPipelinesFunction.AddStatement($"id<MTLDevice> device = GetDevice();");
      createPipelinesFunction.AddStatement($"id<MTLLibrary> defaultLibrary = [device newDefaultLibraryWithBundle:[NSBundle bundleForClass:[{objCPlaceholderClassIdentifier} class]] error:&error];");
      program.AddPreambleStatement($"@interface {objCPlaceholderClassIdentifier} : NSObject");
      program.AddPreambleStatement($"@end");
      program.AddPreambleStatement($"@implementation {objCPlaceholderClassIdentifier}");
      program.AddPreambleStatement($"@end");

      // Prepare to traverse graph of compute nodes.
      var computeNodeResults = new Dictionary<IComputeNode, (CodeCachedResult? result, IComputeNodeEmitCodeOperation op)>();
      Queue<IComputeNode> queuedCalcNodes = new Queue<IComputeNode>();
      HashSet<IComputeNode> wasQueuedCalcNode = new HashSet<IComputeNode>();
      void QueueComputeNode(IComputeNode node) {
        if (!wasQueuedCalcNode.Add(node)) {
          return;
        }
        queuedCalcNodes.Enqueue(node);
      }

      // Queue the final output nodes that we must execute.
      foreach (IComputeNode computeNode in outputNodes) {
        QueueComputeNode(computeNode);
      }

      // Traverse graph of compute nodes and generate a plan for each.
      var computePlans = new List<(IComputeNode node, List<ICodeGenerator>, List<CodeGeneratorFromComputeNode>, List<DataPlug>)>();
      while (queuedCalcNodes.TryDequeue(out IComputeNode node)) {
        List<CodeGeneratorFromComputeNode> dependentComputeNodes = new List<CodeGeneratorFromComputeNode>();
        List<DataPlug> dependentComputeInputs = new List<DataPlug>();
        List<ICodeGenerator> computePlan = GenerateComputePlan(node, dependentComputeNodes, dependentComputeInputs, errors);
        if (node is ISplitComputeNode) {
          if (dependentComputeNodes.Count > 1) {
            errors.Add($"Node {node} is in multiple compute nodes. It must depend on exactly one node.");
            continue;
          }
          // TODO: Verify that the dependee is actually the same as the dependency.
          // Do not actually compute anything for this node.
          computePlan.Clear();
          dependentComputeNodes.Clear();
          dependentComputeInputs.Clear();
        }
        computePlans.Add((node, computePlan, dependentComputeNodes, dependentComputeInputs));
        foreach (var dependency in dependentComputeNodes) {
          QueueComputeNode(dependency.Node);
        }
      }
      computePlans.Reverse();

      // Execute each compute plan in order.
      foreach (var (computeNode, computePlan, dependentComputeNodes, dependentComputeInputs) in computePlans) {
        // Read inputs.
        var dependentComputeNodeResults = new List<ComputeNodeResultEntry>();
        foreach (var dependency in dependentComputeNodes) {
          if (!computeNodeResults.TryGetValue(dependency.Node, out var result)) {
            errors.Add($"Dependency {dependency.Node} for {computeNode} not yet ready.");
            continue;
          }
          dependency.SetInput(result.result ?? default, result.op);
          dependentComputeNodeResults.Add(new ComputeNodeResultEntry { Node = dependency.Node, Result = result.result, Operation = result.op });
        }

        var emitContext = new ComputeNodeEmitCodeOperationContext {
            errors = errors,
            graph = this,
            program = program,
            executeFunction = executeFunction,
            createPipelinesFunction = createPipelinesFunction,
            dependentComputeNodes = dependentComputeNodeResults,
            dependentComputeInputs = dependentComputeInputs,
        };
        var emitOp = computeNode.CreateEmitCodeOperation(emitContext);

        emitOp.EmitFunctionSignature();
        emitOp.EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc);
        emitOp.EmitLoadFunctionInputs();

        Dictionary<DataPlug, CodeLocal> resultLocalMap = new Dictionary<DataPlug, CodeLocal>();
        CodeLocal[] GetInputLocals(IDataNode sourceNode, DataSpec inputSpec) {
          var inputSpecFields = inputSpec.Fields.Where(field => !field.IsCompileTimeOnly).ToArray();

          CodeLocal[] inputLocals = new CodeLocal[inputSpecFields.Length];
          for (int i = 0; i < inputSpecFields.Length; ++i) {
            var field = inputSpecFields[i];
            if (field.IsCompileTimeOnly) {
              continue;
            }
            var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = sourceNode, FieldName = field.Name });
            if (edge == null) {
              // Input is not connected. This may or may not be desired.
              continue;
            }
            CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
            if (inputLocal == null) {
              errors.Add($"Input {field.Name} for {sourceNode} is not defined.");
              continue;
            }

            TypeSpec inputType = inputLocal?.Type ?? default;
            TypeSpec desiredType = field.Type;
            var inputProgramType = program.GetProgramType(inputType);
            var desiredProgramType = program.GetProgramType(desiredType);

            string actualInputLocal = inputLocal?.Identifier;
            TypeSpec actualType = inputType;
            if (inputProgramType != desiredProgramType) {
              actualInputLocal = func.AllocLocal("Converted");
              func.AddStatement($"{func.GetTypeIdentifier(desiredProgramType)} {actualInputLocal} = {func.EmitConvert(inputProgramType, desiredProgramType, inputLocal?.Identifier)};");
              actualType = desiredType;
            }

            inputLocals[i] = new CodeLocal { Identifier = actualInputLocal, Type = actualType, ArraySizeIdentifier = inputLocal?.ArraySizeIdentifier };
          }
          return inputLocals;
        }

        // Emit the meat of the function.
        // Dictionary<DataPlug, string> resultSizesLocalMap = new Dictionary<DataPlug, string>();
        foreach (ICodeGenerator codeGenerator in computePlan) {
          var inputSpecFields = codeGenerator.InputSpec.Fields.Where(field => !field.IsCompileTimeOnly).ToArray();
          var outputSpec = codeGenerator.OutputSpec;

          CodeLocal[] inputLocals = GetInputLocals(codeGenerator.SourceNode, codeGenerator.InputSpec);

          List<CodeLocal> outputLocals =  new List<CodeLocal>();
          foreach (var field in outputSpec.Fields) {
            if (field.IsCompileTimeOnly) {
              continue;
            }
            string outputLocal = func.AllocLocal($"{codeGenerator.SourceNode.ShortName}_{field.Name}");
            string outputSizeLocal = arraySizesFunc.AllocLocal($"{codeGenerator.SourceNode.ShortName}_{field.Name}");
            resultLocalMap[new DataPlug { Node = codeGenerator.SourceNode, FieldName = field.Name }] = new CodeLocal { Identifier = outputLocal, Type = field.Type, ArraySizeIdentifier = outputSizeLocal };
            outputLocals.Add(new CodeLocal { Identifier = outputLocal, Type = field.Type, ArraySizeIdentifier = outputSizeLocal });
          }

          codeGenerator.EmitCode(new CodeContext {
            Function = func,
            ArraySizeFunction = arraySizesFunc,
            InputLocals = inputLocals,
            OutputLocals = outputLocals,
            Errors = errors,
          });
        }

        var splitComputeDependencies = dependentComputeNodes.Where(dependency => dependency.SourceNode is ISplitComputeNode).ToArray();
        foreach (var dependency in splitComputeDependencies) {
          ISplitComputeNode splitComputeNode = dependency.SourceNode as ISplitComputeNode;
          if (dependency.Operation == null) {
            errors.Add($"Node {splitComputeNode} is not ready.");
            continue;
          }
          if (!(dependency.Operation is ISplitComputeNodeEmitCodeOperation op)) {
            errors.Add($"Node {splitComputeNode} does not implement {nameof(ISplitComputeNodeEmitCodeOperation)}.");
            continue;
          }

          CodeLocal[] inputLocals = GetInputLocals(splitComputeNode, splitComputeNode.InputSpec);
          op.EmitLateUpdateCode(computeNode, new CodeContext {
            Function = func,
            ArraySizeFunction = arraySizesFunc,
            InputLocals = inputLocals,
            OutputLocals = Array.Empty<CodeLocal>(),
            Errors = errors,
          });
        }

        emitOp.ConsumeFunctionBodyResult(resultLocalMap);
        emitOp.EmitFunctionReturn(out CodeCachedResult? codeCachedResult);
        emitOp.EmitValidateSizesCacheFunction();
        emitOp.EmitValidateCacheFunction();
        emitOp.EmitExecuteFunctionCode();
        computeNodeResults[computeNode] = (codeCachedResult, emitOp);
      }

      // Store final outputs.
      int finalOutputIndex = 0;
      foreach (var outputNode in outputNodes) {
        var outputSpec = outputNode.OutputSpec;
        if (!computeNodeResults.TryGetValue(outputNode, out var cachedResult) || cachedResult.result == null) {
          errors.Add($"Missing result for output node {outputNode}");
          continue;
        }
        foreach (var field in outputSpec.Fields) {
          if (field.IsCompileTimeOnly) {
            continue;
          }
          string fieldIdentifier = cachedResult.result?.ResultType.GetField(field.Name);
          var getOutputFunc = program.AddOverrideFunction($"GetOutput{finalOutputIndex}", NanoProgram.CpuContext, program.GetProgramType(field.Type));
          getOutputFunc.AddStatement($"return {cachedResult.result?.Result.Identifier}.{fieldIdentifier};");
          finalOutputIndex++;
        }

        var getTextureInputCountFunc = program.AddOverrideFunction($"GetTextureInputCount", NanoProgram.CpuContext, program.IntType, modifiers: new[] { "virtual" });
        getTextureInputCountFunc.AddStatement($"return {getTextureInputCountFunc.EmitLiteral(program.TextureInputCount)};");

        var getParamsFunc = program.AddOverrideFunction($"GetParameterDecls", NanoProgram.CpuContext, NanoProgramType.MakeBuiltIn(program, "std::vector<ParameterDecl>"), modifiers: new[] { "virtual" });
        getParamsFunc.AddStatement("std::vector<ParameterDecl> parameters = {");
        foreach (var valueInput in program.ValueInputs) {
          getParamsFunc.AddStatement($"  ParameterDecl {{");
          getParamsFunc.AddStatement($"    .Name = {getParamsFunc.EmitLiteral(valueInput.Name)},");
          getParamsFunc.AddStatement($"    .DefaultValue = {getParamsFunc.EmitLiteral(valueInput.DefaultValue)},");
          getParamsFunc.AddStatement($"    .MinValue = {getParamsFunc.EmitLiteral(valueInput.MinValue)},");
          getParamsFunc.AddStatement($"    .MaxValue = {getParamsFunc.EmitLiteral(valueInput.MaxValue)},");
          getParamsFunc.AddStatement($"  }}");
        }
        getParamsFunc.AddStatement("};");
        getParamsFunc.AddStatement($"return parameters;");
      }

      string outerCpuCode = program.OuterCpuCode;
      Debug.Log(outerCpuCode);
      string outerGpuCode = program.OuterGpuCode;
      Debug.Log(outerGpuCode);

      System.IO.File.WriteAllText("/Users/nattos/Desktop/Program.incl.h", outerCpuCode);
      System.IO.File.WriteAllText("/Users/nattos/Desktop/Program.metal.incl.h", outerGpuCode);

      if (errors.Count > 0) {
        Debug.Log(string.Join("\n", errors));
      }
    }

    private static DataField[] CompileTimeOnlyFields(IEnumerable<DataField> fields) {
      return fields.Where(field => !field.IsCompileTimeOnly).ToArray();
    }

    private static readonly Lazy<NanoGraph> _debugInstance = new Lazy<NanoGraph>(() => {
      return new NanoGraph { EffectName = "Program" };
    });
    public static NanoGraph DebugInstance => _debugInstance.Value;









    [UnityEditor.MenuItem("Do/Something")]
    public static void Test() {
      new NanoGraph().DoSomething();
    }

    public void DoSomething() {

      // VectorIndexNode calc3VectorIndex = new VectorIndexNode { Name = "Calc3 VectorIndexNode" };
      // ReadNode calc3ReadNode = new ReadNode { Name = "Calc3 ReadNode", ElementType = TypeSpec.MakePrimitive(PrimitiveType.Float) };
      // MathNode calc3AddInputs = MathNode.Make(PrimitiveType.Float, StandardOperatorSpec.MakeBinary(StandardBinaryOperator.Add)); calc3AddInputs.Name = "Calc3 Add";
      // VectorComputeNode calc3OutputNode = new VectorComputeNode { InputOutputSpec = DataSpec.FromFields(DataField.MakeType("Some Value", TypeSpec.MakeArray(TypeSpec.MakePrimitive(PrimitiveType.Float)))), Name = "Calc3" };
      // // LiteralNode lhsInput = new LiteralNode(PrimitiveType.Float) { Name = "Calc1 LHS" };
      // // LiteralNode rhsInput = new LiteralNode(PrimitiveType.Float) { Name = "Calc1 RHS" };
      // // MathNode addInputs = MathNode.Make(PrimitiveType.Float, StandardOperatorSpec.MakeBinary(StandardBinaryOperator.Add)); addInputs.Name = "Calc1 Add";
      // // CalcStageNode outputNode = CalcStageNode.MakeFromDataSpec(DataSpec.FromFields(DataField.MakePrimitive("Some Value", PrimitiveType.Float))); outputNode.Name = "Calc1";

      // // LiteralNode calc2Input = new LiteralNode(PrimitiveType.Float) { Name = "Calc2 Value" };
      // // CalcStageNode calc2OutputNode = CalcStageNode.MakeFromDataSpec(DataSpec.FromFields(DataField.MakePrimitive("Some Value", PrimitiveType.Float))); calc2OutputNode.Name = "Calc2";
      // LiteralNode lhsInput = new LiteralNode(PrimitiveType.Float) { Name = "Calc1 LHS" };
      // LiteralNode rhsInput = new LiteralNode(PrimitiveType.Float) { Name = "Calc1 RHS" };
      // MakeArrayNode addInputs = MakeArrayNode.Make(TypeSpec.MakePrimitive(PrimitiveType.Float), 2); addInputs.Name = "Calc1 MakeArray";
      // ConcatNode addInputs2 = ConcatNode.Make(TypeSpec.MakePrimitive(PrimitiveType.Float), 2); addInputs2.Name = "Calc1 Concat";
      // // LiteralNode readIndex = new LiteralNode(PrimitiveType.Float) { Name = "Calc1 readIndex" };
      // // ReadNode readInput = new ReadNode { ElementType = TypeSpec.MakePrimitive(PrimitiveType.Float), Name = "Calc1 ReadNode" };
      // ScalarComputeNode outputNode = new ScalarComputeNode { InputOutputSpec = DataSpec.FromFields(DataField.MakeType("Some Value", TypeSpec.MakeArray(TypeSpec.MakePrimitive(PrimitiveType.Float)))), Name = "Calc1" };

      // LiteralNode calc2Input = new LiteralNode(PrimitiveType.Float) { Name = "Calc2 Value", Value = -1 };
      // ScalarComputeNode calc2OutputNode = new ScalarComputeNode { InputOutputSpec = DataSpec.FromFields(DataField.MakePrimitive("Some Value", PrimitiveType.Float)), Name = "Calc2" };
      // AddNode(calc2Input);
      // AddNode(calc2OutputNode);
      // Connect(calc2Input, "Out", calc2OutputNode, "Some Value");

      // AddNode(lhsInput);
      // AddNode(rhsInput);
      // AddNode(addInputs);
      // AddNode(addInputs2);
      // // AddNode(readIndex);
      // // AddNode(readInput);
      // AddNode(outputNode);
      // // Connect(lhsInput, "Out", addInputs, "0");
      // Connect(calc2OutputNode, "Some Value", addInputs, "0");
      // Connect(rhsInput, "Out", addInputs, "1");
      // Connect(addInputs, "Out", addInputs2, "0");
      // Connect(addInputs, "Out", addInputs2, "1");
      // // Connect(addInputs2, "Out", readInput, "In");
      // // Connect(readIndex, "Out", readInput, "Index");
      // // Connect(readInput, "Out", outputNode, "Some Value");
      // Connect(addInputs2, "Out", outputNode, "Some Value");

      // AddNode(calc3VectorIndex);
      // AddNode(calc3ReadNode);
      // AddNode(calc3AddInputs);
      // AddNode(calc3OutputNode);
      // Connect(outputNode, "Some Value", calc3ReadNode, "In");
      // Connect(calc3VectorIndex, "Out", calc3ReadNode, "Index");
      // Connect(calc3ReadNode, "Out", calc3AddInputs, "0");
      // Connect(calc3ReadNode, "Out", calc3AddInputs, "1");
      // Connect(calc3AddInputs, "Out", calc3OutputNode, "Some Value");

      T AddNewNode<T>(T node) where T : IDataNode {
        AddNode(node);
        return node;
      }

      var typeStandardVertex = new TypeDecl(TypeField.MakePrimitive("position", PrimitiveType.Float2),
                                            TypeField.MakePrimitive("color", PrimitiveType.Float4),
                                            TypeField.MakePrimitive("uv", PrimitiveType.Float2));
      var typeStandardVertexNode = AddNewNode(new TypeDeclNode { EditableFields = {
        new TypeDeclNode.Field { Name = "position", Primitive = PrimitiveType.Float2 },
        new TypeDeclNode.Field { Name = "color", Primitive = PrimitiveType.Float4 },
        new TypeDeclNode.Field { Name = "uv", Primitive = PrimitiveType.Float2 },
      } });

      var vertex1Cpu = AddNewNode(new LiteralNode { Name = "vertex1Cpu", Type = PrimitiveType.Float3, ValueSource = InputSource.Internal, InternalValue = new Vector3(0.0f, 0.0f, 0.0f) });
      var vertex2Cpu = AddNewNode(new LiteralNode { Name = "vertex2Cpu", Type = PrimitiveType.Float3, ValueSource = InputSource.Internal, InternalValue = new Vector3(1.0f, 1.0f, 0.0f) });
      var vertex3Cpu = AddNewNode(new LiteralNode { Name = "vertex3Cpu", Type = PrimitiveType.Float3, ValueSource = InputSource.Internal, InternalValue = new Vector3(0.0f, 1.0f, 0.0f) });
      var vertex4Cpu = AddNewNode(new LiteralNode { Name = "vertex4Cpu", Type = PrimitiveType.Float3, ValueSource = InputSource.Internal, InternalValue = new Vector3(0.0f, 0.0f, 0.0f) });
      var vertex5Cpu = AddNewNode(new LiteralNode { Name = "vertex5Cpu", Type = PrimitiveType.Float3, ValueSource = InputSource.Internal, InternalValue = new Vector3(-1.0f, -1.0f, 0.0f) });
      var vertex6Cpu = AddNewNode(new LiteralNode { Name = "vertex6Cpu", Type = PrimitiveType.Float3, ValueSource = InputSource.Internal, InternalValue = new Vector3(0.0f, -1.0f, 0.0f) });
      var makeArrayCpu = AddNewNode(new MakeArrayNode { Name = "makeArrayCpu", InputCount = 6 });
      var cpuNode = AddNewNode(new ScalarComputeNode { Name = "cpuNode", InternalType = new TypeDecl(TypeField.MakeType("0", TypeSpec.MakeArray(TypeSpec.MakePrimitive(PrimitiveType.Float3)))), TypeDeclMode = TypeDeclMode.Internal });
      Connect(vertex1Cpu, "Out", makeArrayCpu, "0");
      Connect(vertex2Cpu, "Out", makeArrayCpu, "1");
      Connect(vertex3Cpu, "Out", makeArrayCpu, "2");
      Connect(vertex4Cpu, "Out", makeArrayCpu, "3");
      Connect(vertex5Cpu, "Out", makeArrayCpu, "4");
      Connect(vertex6Cpu, "Out", makeArrayCpu, "5");
      Connect(makeArrayCpu, "Out", cpuNode, "0");

      var vectorIndexGpu = AddNewNode(new VectorIndexNode { Name = "vectorIndexGpu" });
      var readInputGpu = AddNewNode(new ReadNode { Name = "readInputGpu" });
      var makeStandardVertexGpu = AddNewNode(new PackNode { Name = "makeStandardVertexGpu" });
      var gpuNode = AddNewNode(new VectorComputeNode { Name = "gpuNode", ThreadCountMode = ThreadCountMode.ArraySize });
      Connect(cpuNode, "0", readInputGpu, "In");
      Connect(vectorIndexGpu, "Out", readInputGpu, "Index");
      Connect(typeStandardVertexNode, "Out", makeStandardVertexGpu, "TypeDecl");
      Connect(readInputGpu, "Out", makeStandardVertexGpu, "position");
      Connect(readInputGpu, "Out", makeStandardVertexGpu, "color");
      Connect(readInputGpu, "Out", makeStandardVertexGpu, "uv");
      Connect(typeStandardVertexNode, "Out", gpuNode, "TypeDecl");
      Connect(makeStandardVertexGpu, "Out", gpuNode, "Out");
      Connect(cpuNode, "0", gpuNode, "ThreadCountFromArray");


      Validate();
      GenerateProgram(new[] { gpuNode });
    }

  }
}
