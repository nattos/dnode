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
    private readonly Dictionary<DataPlug, DataEdge> _edgesBySource = new Dictionary<DataPlug, DataEdge>();
    private readonly Dictionary<DataPlug, DataEdge> _edgesByDest = new Dictionary<DataPlug, DataEdge>();

    private readonly Dictionary<IDataNode, List<Action>> _nodeInvalidatedHanders = new Dictionary<IDataNode, List<Action>>();

    public void AddNode(IDataNode node) {
      Debug.Log($"Add Node: {node}");
      node.Graph?.RemoveNode(node);
      node.Graph = this;
      node.CacheData = new DataNodeCacheData();
      _nodes.Add(node);
      _dirtyNodes.Add(node);
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

      // TODO: Allow multiple output connections.
      RemoveEdgeForPlug(sourcePlug, _edgesBySource, _outputEdgesForNode);
      RemoveEdgeForPlug(destPlug, _edgesByDest, _inputEdgesForNode);

      DataEdge edge = new DataEdge { Source = sourcePlug, Destination = destPlug };
      _edges.Add(edge);
      AddEdgeForPlug(sourcePlug, edge, _edgesBySource, _outputEdgesForNode);
      AddEdgeForPlug(destPlug, edge, _edgesByDest, _inputEdgesForNode);

      _dirtyNodes.Add(sourceNode);
      _dirtyNodes.Add(destNode);

      Debug.Log($"Connected {sourcePlug} => {destPlug}");
    }

    public void Disconnect(IDataNode sourceNode, string sourcePlugName, IDataNode destNode, string destPlugName) {
      if (!_edgesByDest.TryGetValue(new DataPlug { Node = destNode, FieldName = destPlugName }, out DataEdge edge)) {
        return;
      }

      RemoveEdgeForPlug(edge.Source, _edgesBySource, _outputEdgesForNode);
      RemoveEdgeForPlug(edge.Destination, _edgesByDest, _inputEdgesForNode);
      _edges.Remove(edge);

      _dirtyNodes.Add(edge.Source.Node);
      _dirtyNodes.Add(edge.Destination.Node);
      Debug.Log($"Disconnected {edge.Source} => {edge.Destination}");
    }

    private void RemoveEdgeForPlug(DataPlug plug, Dictionary<DataPlug, DataEdge> edgeMap, Dictionary<IDataNode, List<DataEdge>> nodeEdgeMap) {
      if (edgeMap.TryGetRemove(plug, out DataEdge edge)) {
        _edges.Remove(edge);
      }
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

    public void Validate() {
      List<Action> cleanupActions = new List<Action>();

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
        var inputSpec = node.InputSpec;
        var outputSpec = node.OutputSpec;
        var cacheData = node.CacheData;

        // TODO: Run custom validation.
        node.Validate(cleanupActions);

        // Check if input spec changed.
        if (!StructuralComparisons.StructuralEqualityComparer.Equals(cacheData.InputSpec, inputSpec.Fields)) {
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
        if (!StructuralComparisons.StructuralEqualityComparer.Equals(cacheData.OutputSpec, outputSpec.Fields)) {
          invalidatedNodes.Add(node);
          cacheData.OutputSpec = outputSpec.Fields.ToArray();
          // Invalidate all output connections.
          foreach (var field in outputSpec.Fields) {
            var key = new DataPlug { Node = node, FieldName = field.Name };
            if (_edgesBySource.TryGetValue(key, out DataEdge edge)) {
              TryEnqueue(edge.Destination.Node);
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
          if (_edgesBySource.TryGetValue(key, out DataEdge edge)) {
            ValidateFieldsMatch(edge, field, edge.DestinationFieldOrNull);
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
        if (node is ICodeNode codeNode) {
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
      var executeFunction = program.AddOverrideFunction("Execute", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.VoidType);
      var createPipelinesFunction = program.AddOverrideFunction("CreatePipelines", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.VoidType);
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

        // Emit the meat of the function.
        Dictionary<DataPlug, CodeLocal> resultLocalMap = new Dictionary<DataPlug, CodeLocal>();
        // Dictionary<DataPlug, string> resultSizesLocalMap = new Dictionary<DataPlug, string>();
        foreach (ICodeGenerator codeGenerator in computePlan) {
          var inputSpecFields = codeGenerator.InputSpec.Fields.Where(field => !field.IsCompileTimeOnly).ToArray();
          var outputSpec = codeGenerator.OutputSpec;

          CodeLocal[] inputLocals = new CodeLocal[inputSpecFields.Length];
          for (int i = 0; i < inputSpecFields.Length; ++i) {
            var field = inputSpecFields[i];
            if (field.IsCompileTimeOnly) {
              continue;
            }
            var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = codeGenerator.SourceNode, FieldName = field.Name });
            if (edge == null) {
              // Input is not connected. This may or may not be desired.
              continue;
            }
            CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
            if (inputLocal == null) {
              errors.Add($"Input {field.Name} for {codeGenerator.SourceNode} is not defined.");
              continue;
            }

            TypeSpec inputType = edge.SourceFieldOrNull?.Type ?? default;
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
            // CompileTimeOnlyInputs = compileTimeOnlyInputs,
            Errors = errors,
          });
        }

        emitOp.ConsumeFunctionBodyResult(resultLocalMap);
        emitOp.EmitFunctionReturn(out CodeCachedResult? codeCachedResult);
        emitOp.EmitValidateSizesCacheFunction();
        emitOp.EmitValidateCacheFunction();
        emitOp.EmitExecuteFunctionCode();
        computeNodeResults[computeNode] = (codeCachedResult, emitOp);

        // // Define a type to hold the result value.
        // DataSpec computeInputSpec = computeNode.ComputeInputSpec;
        // DataSpec computeArrayInputSpec = computeNode.AuxSizesOutputSpec;
        // DataSpec computeOutputSpec = computeNode.OutputSpec;
        // TypeDecl resultTypeDecl = TypeDeclFromDataFields(computeOutputSpec.Fields);
        // DataField[] compileTimeOnlyInputs = CompileTimeOnlyFields(computeInputSpec.Fields);
        // TypeSpec resultTypeSpec = TypeSpec.MakeType(resultTypeDecl);
        // NanoProgramType resultType = program.AddType(resultTypeDecl, $"Result_{computeNode.ShortName}");
        // NanoProgramType arraySizeResultType = program.AddType(TypeDeclFromDataFields(computeOutputSpec.Fields.Concat(computeArrayInputSpec.Fields).Select(field => new DataField { Name = field.Name, Type = TypeSpec.MakePrimitive(PrimitiveType.Int) }).ToArray()), $"ResultSizes_{computeNode.ShortName}");
        // // Define a field to hold the cached result.
        // string cachedResultIdentifier = program.AddInstanceField(resultType, $"Result_{computeNode.ShortName}");
        // string cachedResultSizesIdentifier = program.AddInstanceField(arraySizeResultType, $"ResultSizes_{computeNode.ShortName}");

        // // Begin generating the main results function.
        // var funcContext = computeNode.CodeContext;
        // bool isGpuContext = funcContext is NanoGpuContext;
        // string[] functionModifiers = isGpuContext ? new[] { "kernel" } : Array.Empty<string>();
        // var func = program.AddFunction(computeNode.ShortName, funcContext, paramTypes: Array.Empty<NanoProgramType>(), isGpuContext ? program.VoidType : resultType, functionModifiers);
        // List<string> dependentInputBuffers = new List<string>();
        // foreach (var dependency in dependentCalcNodes) {
        //   if (!computeNodeResults.TryGetValue(dependency.Node, out var result)) {
        //     errors.Add($"Dependency {dependency.Node} for {computeNode} not yet ready.");
        //     continue;
        //   }
        //   string resultIdentifier = dependency.InputResult.Value.Result.Identifier;
        //   foreach (var field in dependency.Node.OutputSpec.Fields) {
        //     var fieldType = program.GetProgramType(field.Type, field.Name);
        //     if (fieldType.IsArray) {
        //       dependentInputBuffers.Add($"{resultIdentifier}.{dependency.InputResult.Value.ResultType.GetField(field.Name)}");
        //     }
        //   }
        // }
        // List<(string, int, TypeSpec)> gpuInputBuffers = new List<(string, int, TypeSpec)>();
        // List<(string, int, TypeSpec)> gpuOutputBuffers = new List<(string, int, TypeSpec)>();
        // if (isGpuContext) {
        //   // Load inputs.
        //   // TODO: Only load inputs that we really read.
        //   int bufferIndex = 0;
        //   int inputIndex = 0;
        //   foreach (var dependency in dependentCalcNodes) {
        //     if (!computeNodeResults.TryGetValue(dependency.Node, out var result)) {
        //       errors.Add($"Dependency {dependency.Node} for {computeNode} not yet ready.");
        //       continue;
        //     }
        //     string resultIdentifier = dependency.InputResult.Value.Result.Identifier;
        //     foreach (var field in dependency.Node.OutputSpec.Fields) {
        //       if (field.IsCompileTimeOnly) {
        //         continue;
        //       }
        //       gpuInputBuffers.Add(($"{resultIdentifier}.{dependency.InputResult.Value.ResultType.GetField(field.Name)}", bufferIndex, field.Type));
        //       var fieldType = program.GetProgramType(field.Type, field.Name);
        //       string[] modifiers = { "constant", "const" };
        //       string suffix = $"[[buffer({bufferIndex++})]]";
        //       bool isReference = true;
        //       if (fieldType.IsArray) {
        //         modifiers = Array.Empty<string>();
        //         isReference = false;
        //       }
        //       func.AddParam(modifiers, fieldType, $"input{inputIndex++}", suffix, new NanoParameterOptions { IsConst = true, IsReference = isReference });
        //     }
        //   }

        //   // Define outputs.
        //   for (int i = 0; i < computeOutputSpec.Fields.Count; ++i) {
        //     var field = computeOutputSpec.Fields[i];
        //     if (field.IsCompileTimeOnly) {
        //       continue;
        //     }
        //     var fieldType = field.Type;
        //     gpuOutputBuffers.Add((field.Name, bufferIndex, fieldType));
        //     string[] modifiers = {};
        //     string suffix = $"[[buffer({bufferIndex++})]]";
        //     if (fieldType.IsArray) {
        //       modifiers = Array.Empty<string>();
        //     }
        //     func.AddParam(modifiers, program.GetProgramType(fieldType, field.Name), $"output{i}", suffix, new NanoParameterOptions { IsConst = false });
        //   }
        //   func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint), $"gid_uint", "[[thread_position_in_grid]]");
        //   func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");
        // }
        // var arraySizesFunc = program.AddFunction($"{computeNode.ShortName}_Sizes", NanoProgram.CpuContext, paramTypes: Array.Empty<NanoProgramType>(), arraySizeResultType);

        // Dictionary<DataPlug, string> resultLocalMap = new Dictionary<DataPlug, string>();
        // Dictionary<DataPlug, string> resultSizesLocalMap = new Dictionary<DataPlug, string>();
        // foreach (ICodeGenerator codeGenerator in computePlan) {
        //   var inputSpecFields = codeGenerator.InputSpec.Fields.Where(field => !field.IsCompileTimeOnly).ToArray();
        //   var outputSpec = codeGenerator.OutputSpec;

        //   CodeLocal[] inputLocals = new CodeLocal[inputSpecFields.Length];
        //   for (int i = 0; i < inputSpecFields.Length; ++i) {
        //     var field = inputSpecFields[i];
        //     if (field.IsCompileTimeOnly) {
        //       continue;
        //     }
        //     var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = codeGenerator.SourceNode, FieldName = field.Name });
        //     if (edge == null) {
        //       // Input is not connected. This may or may not be desired.
        //       continue;
        //     }
        //     string inputLocal = resultLocalMap.GetOrDefault(edge.Source);
        //     string inputSizeLocal = resultSizesLocalMap.GetOrDefault(edge.Source);
        //     if (inputLocal == null || inputSizeLocal == null) {
        //       errors.Add($"Input {field.Name} for {codeGenerator.SourceNode} is not defined.");
        //       continue;
        //     }

        //     TypeSpec inputType = edge.SourceFieldOrNull?.Type ?? default;
        //     TypeSpec desiredType = field.Type;
        //     var inputProgramType = program.GetProgramType(inputType);
        //     var desiredProgramType = program.GetProgramType(desiredType);

        //     string actualInputLocal = inputLocal;
        //     TypeSpec actualType = inputType;
        //     if (inputProgramType != desiredProgramType) {
        //       actualInputLocal = func.AllocLocal("Converted");
        //       func.AddStatement($"{func.GetTypeIdentifier(desiredProgramType)} {actualInputLocal} = {func.EmitConvert(inputProgramType, desiredProgramType, inputLocal)};");
        //       actualType = desiredType;
        //     }

        //     inputLocals[i] = new CodeLocal { Identifier = actualInputLocal, Type = actualType, ArraySizeIdentifier = inputSizeLocal };
        //   }

        //   List<CodeLocal> outputLocals =  new List<CodeLocal>();
        //   foreach (var field in outputSpec.Fields) {
        //     if (field.IsCompileTimeOnly) {
        //       continue;
        //     }
        //     string outputLocal = func.AllocLocal($"{codeGenerator.SourceNode.ShortName}_{field.Name}");
        //     string outputSizeLocal = arraySizesFunc.AllocLocal($"{codeGenerator.SourceNode.ShortName}_{field.Name}");
        //     resultLocalMap[new DataPlug { Node = codeGenerator.SourceNode, FieldName = field.Name }] = outputLocal;
        //     resultSizesLocalMap[new DataPlug { Node = codeGenerator.SourceNode, FieldName = field.Name }] = outputSizeLocal;
        //     outputLocals.Add(new CodeLocal { Identifier = outputLocal, Type = field.Type, ArraySizeIdentifier = outputSizeLocal });
        //   }

        //   codeGenerator.EmitCode(new CodeContext {
        //     Function = func,
        //     ArraySizeFunction = arraySizesFunc,
        //     InputLocals = inputLocals,
        //     OutputLocals = outputLocals,
        //     // CompileTimeOnlyInputs = compileTimeOnlyInputs,
        //     Errors = errors,
        //   });
        // }

        // if (isGpuContext) {
        //   // TODO: Store results.
        //   string returnSizesLocal = arraySizesFunc.AllocLocal("Return");
        //   var cachedResult = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { Identifier = cachedResultIdentifier, Type = resultTypeSpec, ArraySizeIdentifier = cachedResultSizesIdentifier } };
        //   arraySizesFunc.AddStatement($"{arraySizesFunc.GetTypeIdentifier(arraySizeResultType)} {returnSizesLocal};");

        //   var auxSizesOutputSpec = computeNode.AuxSizesOutputSpec;
        //   CodeLocal[] auxSizesCodeInputLocals = new CodeLocal[auxSizesOutputSpec.Fields.Count];
        //   for (int i = 0; i < auxSizesOutputSpec.Fields.Count; ++i) {
        //     var field = auxSizesOutputSpec.Fields[i];
        //     if (field.IsCompileTimeOnly) {
        //       continue;
        //     }
        //     var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = computeNode, FieldName = field.Name });
        //     if (edge == null) {
        //       continue;
        //     }
        //     if (!(edge.Source.Node is IComputeNode sourceComputeNode)) {
        //       errors.Add($"Node {computeNode} depends on an output that is not a compute node ({edge.Source.Node}).");
        //       continue;
        //     }
        //     if (!computeNodeResults.TryGetValue(sourceComputeNode, out CodeCachedResult sourceCachedResult)) {
        //       errors.Add($"Node {computeNode} depends on a compute node that is not yet ready ({edge.Source.Node}).");
        //       continue;
        //     }
        //     string inputLocal = $"{sourceCachedResult.Result.Identifier}.{sourceCachedResult.ArraySizesResultType.GetField(edge.Source.FieldName)}";
        //     string inputSizeLocal = resultSizesLocalMap.GetOrDefault(edge.Source);
        //     if (inputLocal == null || inputSizeLocal == null) {
        //       errors.Add($"Input {field.Name} for {computeNode} is not defined.");
        //       continue;
        //     }
        //     auxSizesCodeInputLocals[i] = new CodeLocal { Identifier = inputLocal, Type = sourceCachedResult.Result.Type, ArraySizeIdentifier = inputSizeLocal };
        //   }
        //   var emitStoreAuxSizesCodeCachedResult = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { ArraySizeIdentifier = returnSizesLocal } };
        //   computeNode.EmitStoreAuxSizesCode(new CodeContext {
        //     Function = func,
        //     ArraySizeFunction = arraySizesFunc,
        //     InputLocals = auxSizesCodeInputLocals,
        //     OutputLocals = null,
        //     // CompileTimeOnlyInputs = compileTimeOnlyInputs,
        //     Errors = errors,
        //   }, emitStoreAuxSizesCodeCachedResult);

        //   // TODO: Map inputs to outputs somehow.
        //   for (int i = 0; i < computeOutputSpec.Fields.Count; ++i) {
        //     var field = computeOutputSpec.Fields[i];
        //     if (field.IsCompileTimeOnly) {
        //       continue;
        //     }
        //     var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = computeNode, FieldName = field.Name });
        //     if (edge == null) {
        //       continue;
        //     }
        //     string inputLocal = resultLocalMap.GetOrDefault(edge.Source);
        //     string inputSizeLocal = resultSizesLocalMap.GetOrDefault(edge.Source);
        //     if (inputLocal == null || inputSizeLocal == null) {
        //       errors.Add($"Input {field.Name} for {computeNode} is not defined.");
        //       continue;
        //     }
        //     if (field.Type.IsArray) {
        //       func.AddStatement($"{func.Context.EmitWriteBuffer($"output{i}", func.Context.EmitThreadId(), inputLocal)};");
        //     } else {
        //       func.AddStatement($"output{i} = {inputLocal};");
        //     }
        //     // arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {inputSizeLocal};");
        //     arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {computeNode.EmitTotalThreadCount(arraySizesFunc, cachedResult)};");
        //   }
        //   arraySizesFunc.AddStatement($"return {returnSizesLocal};");
        //   computeNodeResults[computeNode] = cachedResult;
        // } else {
        //   string returnLocal = func.AllocLocal("Return");
        //   string returnSizesLocal = arraySizesFunc.AllocLocal("Return");
        //   func.AddStatement($"{func.GetTypeIdentifier(resultType)} {returnLocal};");
        //   arraySizesFunc.AddStatement($"{arraySizesFunc.GetTypeIdentifier(arraySizeResultType)} {returnSizesLocal};");
        //   foreach (var field in computeOutputSpec.Fields) {
        //     if (field.IsCompileTimeOnly) {
        //       continue;
        //     }
        //     var edge = _edgesByDest.GetOrDefault(new DataPlug { Node = computeNode, FieldName = field.Name });
        //     if (edge == null) {
        //       continue;
        //     }
        //     string inputLocal = resultLocalMap.GetOrDefault(edge.Source);
        //     string inputSizeLocal = resultSizesLocalMap.GetOrDefault(edge.Source);
        //     if (inputLocal == null || inputSizeLocal == null) {
        //       errors.Add($"Input {field.Name} for {computeNode} is not defined.");
        //       continue;
        //     }
        //     func.AddStatement($"{returnLocal}.{resultType.GetField(field.Name)} = {inputLocal};");
        //     arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {inputSizeLocal};");
        //   }
        //   func.AddStatement($"return {returnLocal};");
        //   arraySizesFunc.AddStatement($"return {returnSizesLocal};");
        //   computeNodeResults[computeNode] = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { Identifier = cachedResultIdentifier, Type = resultTypeSpec, ArraySizeIdentifier = cachedResultSizesIdentifier } };
        // }

        // NanoFunction validateSizesCacheFunction = program.AddFunction($"UpdateSizes_{computeNode.ShortName}", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.VoidType);
        // validateSizesCacheFunction.AddStatement($"{cachedResultSizesIdentifier} = {arraySizesFunc.Identifier}();");
        // foreach (var field in computeNode.OutputSpec.Fields) {
        //   if (field.IsCompileTimeOnly) {
        //     continue;
        //   }
        //   if (!field.Type.IsArray) {
        //     continue;
        //   }
        //   validateSizesCacheFunction.AddStatement($"if (!{cachedResultIdentifier}.{resultType.GetField(field.Name)}) {{");
        //   validateSizesCacheFunction.AddStatement($"  {cachedResultIdentifier}.{resultType.GetField(field.Name)}.reset(NanoTypedBuffer<{validateSizesCacheFunction.GetElementTypeIdentifier(field.Type)}>::Allocate({cachedResultSizesIdentifier}.{arraySizeResultType.GetField(field.Name)}));");
        //   validateSizesCacheFunction.AddStatement($"}} else {{");
        //   validateSizesCacheFunction.AddStatement($"  {cachedResultIdentifier}.{resultType.GetField(field.Name)}->Resize({cachedResultSizesIdentifier}.{arraySizeResultType.GetField(field.Name)});");
        //   validateSizesCacheFunction.AddStatement($"}}");
        // }

        // NanoFunction validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.VoidType);
        // validateCacheFunction.AddStatement($"{validateSizesCacheFunction.Identifier}();");
        // if (isGpuContext) {
        //   string pipelineStateIdentifier = program.AddInstanceField(program.MTLComputePipelineStateType, $"{computeNode.ShortName}_GpuPipeline");

        //   // TODO: Sync buffers to GPU.
        //   foreach (var (identifier, bufferIndex, fieldType) in gpuInputBuffers) {
        //     if (fieldType.IsArray) {
        //       validateCacheFunction.AddStatement($"{identifier}->SyncToGpu();");
        //     }
        //   }
        //   foreach (var (rawFieldName, bufferIndex, fieldType) in gpuOutputBuffers) {
        //     var fieldName = resultType.GetField(rawFieldName);
        //     if (fieldType.IsArray) {
        //       validateCacheFunction.AddStatement($"{cachedResultIdentifier}.{fieldName}->EnsureGpuBuffer();");
        //     }
        //   }
        //   validateCacheFunction.AddStatement($"id<MTLComputeCommandEncoder> encoder = [GetCurrentCommandBuffer() computeCommandEncoder];");
        //   validateCacheFunction.AddStatement($"[encoder setComputePipelineState:{pipelineStateIdentifier}];"); // TODO: Get pipeline state.
        //   // TODO: Bind buffers.
        //   foreach (var (identifier, bufferIndex, fieldType) in gpuInputBuffers) {
        //     if (fieldType.IsArray) {
        //       validateCacheFunction.AddStatement($"[encoder setBuffer:{identifier}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
        //     } else {
        //       validateCacheFunction.AddStatement($"[encoder setBytes:&{identifier} length:sizeof({identifier}) atIndex:{bufferIndex}];");
        //     }
        //   }
        //   foreach (var (rawFieldName, bufferIndex, fieldType) in gpuOutputBuffers) {
        //     var fieldName = resultType.GetField(rawFieldName);
        //     if (fieldType.IsArray) {
        //       validateCacheFunction.AddStatement($"[encoder setBuffer:{cachedResultIdentifier}.{fieldName}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
        //     }
        //   }
        //   // Run command queue.
        //   validateCacheFunction.AddStatement($"MTLSize batchSize = {{ (NSUInteger)({computeNode.EmitTotalThreadCount(validateCacheFunction, computeNodeResults[computeNode])}), 1, 1 }};");
        //   validateCacheFunction.AddStatement($"MTLSize threadgroupSize = {{ {pipelineStateIdentifier}.maxTotalThreadsPerThreadgroup, 1, 1 }};");
        //   validateCacheFunction.AddStatement($"[encoder dispatchThreads:batchSize threadsPerThreadgroup:threadgroupSize];");
        //   validateCacheFunction.AddStatement($"[encoder endEncoding];");
        //   foreach (var (rawFieldName, bufferIndex, fieldType) in gpuOutputBuffers) {
        //     var fieldName = resultType.GetField(rawFieldName);
        //     if (fieldType.IsArray) {
        //       validateCacheFunction.AddStatement($"{cachedResultIdentifier}.{fieldName}->MarkGpuBufferChanged();");
        //     }
        //   }

        //   // Emit pipeline creation code.
        //   createPipelinesFunction.AddStatement($"{pipelineStateIdentifier} = [device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@\"{func.Identifier}\"] error:&error];");
        // } else {
        //   // Treat buffers correctly.
        //   foreach (string inputBuffer in dependentInputBuffers) {
        //     func.AddStatement($"{inputBuffer}->SyncToCpu();");
        //   }
        //   string funcResultLocal = arraySizesFunc.AllocLocal("Result");
        //   validateCacheFunction.AddStatement($"{validateCacheFunction.GetTypeIdentifier(resultType)} {funcResultLocal} = {func.Identifier}();");
        //   foreach (var field in computeNode.OutputSpec.Fields) {
        //     if (field.IsCompileTimeOnly) {
        //       continue;
        //     }
        //     var fieldName = resultType.GetField(field.Name);
        //     if (field.Type.IsArray) {
        //       validateCacheFunction.AddStatement($"{cachedResultIdentifier}.{fieldName}->CopyCpuFrom({funcResultLocal}.{fieldName}.get());");
        //     } else {
        //       validateCacheFunction.AddStatement($"{cachedResultIdentifier}.{fieldName} = {funcResultLocal}.{fieldName};");
        //     }
        //   }
        // }
        // executeFunction.AddStatement($"{validateCacheFunction.Identifier}();");
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
          var getOutputFunc = program.AddOverrideFunction($"GetOutput{finalOutputIndex}", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.GetProgramType(field.Type));
          getOutputFunc.AddStatement($"return {cachedResult.result?.Result.Identifier}.{fieldIdentifier};");
          finalOutputIndex++;
        }
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