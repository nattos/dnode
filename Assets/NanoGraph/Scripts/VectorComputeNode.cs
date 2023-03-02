using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum ThreadCountMode {
    Integer,
    ArraySize,
  }

  public class VectorComputeNode : ComputeNode, IAutoTypeNode {
    // TODO: Support multiple buffer modes.
    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;

    [EditableAttribute]
    public BasicOutputType OutputType = BasicOutputType.Custom;
    protected override string ShortNamePart => $"{OutputType}VectorCompute";

    [EditableAttribute]
    public TypeDeclBuilder Fields = new TypeDeclBuilder();

    [EditableAttribute]
    public int AtomicCounterFieldIndex = -1;
    [EditableAttribute]
    public int AtomicCounterBufferLength = 1;
    [EditableAttribute]
    public int AtomicCounterFieldIndex2 = -1;
    [EditableAttribute]
    public int AtomicCounterBufferLength2 = 1;
    [EditableAttribute]
    public int ForceClearFieldIndex = -1;

    [EditableAttribute]
    public bool DisableBufferWrite = false;
    [EditableAttribute]
    public int BufferSizeMultiplier = 1;
    [EditableAttribute]
    public int MinBufferSize = 0;
    [EditableAttribute]
    public bool HasBufferRefIn = false;

    protected override PrimitiveType? SingleFieldModeType => OutputType.ToPrimitiveTypeOrNull();
    protected override bool RequiresTypeDeclInput => OutputType == BasicOutputType.Fields ? false : base.RequiresTypeDeclInput;

    public override TypeField[] InputOutputTypeFields {
      get {
        if (OutputType == BasicOutputType.Fields) {
          return Fields?.AsTypeFields() ?? Array.Empty<TypeField>();
        }
        return base.InputOutputTypeFields;
      }
    }

    public override DataSpec ComputeInputSpec => base.ComputeInputSpec;
    public override DataSpec InputSpec => DataSpec.ExtendWithFields(ThreadCountFields.Concat(BufferRefInFields).ToArray(), base.InputSpec);
    protected override bool ForceOutputIsArray => true;

    public DataField[] ThreadCountFields {
      get {
        switch (ThreadCountMode) {
          case ThreadCountMode.Integer:
            return new[] { DataField.MakePrimitive("ThreadCount", PrimitiveType.Int) };
          case ThreadCountMode.ArraySize:
          default:
            return new[] { DataField.MakeType("ThreadCountFromArray", TypeSpec.MakeArray(ThreadCountFromArrayElementType)) };
        }
      }
    }

    public DataField[] BufferRefInFields {
      get {
        if (!HasBufferRefIn) {
          return Array.Empty<DataField>();
        }
        switch (InputPortsMode) {
          case FieldPortsMode.Combined:
          default:
            return new[] {  DataField.MakePrimitive("BufferRef", PrimitiveType.BufferRef).ToCompileTimeOnly() };
          case FieldPortsMode.Individual:
            return base.InputSpec.Fields
                .Where(field => !field.IsCompileTimeOnly)
                .Select(field => DataField.MakePrimitive($"{field.Name}BufferRef", PrimitiveType.BufferRef).ToCompileTimeOnly())
                .ToArray();
        }
      }
    }

    [EditableAttribute]
    public ThreadCountMode ThreadCountMode = ThreadCountMode.Integer;
    [EditableAttribute]
    public AutoType ThreadCountFromArrayAutoType = AutoType.Auto;
    public TypeSpec ThreadCountFromArrayElementType;

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetEdgeToDestinationOrNull(this, "ThreadCountFromArray"), ref ThreadCountFromArrayElementType, forceIsArray: false);
    }

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterGpu(this, context);
  
    private class EmitterGpu : EmitterBase {
      public readonly VectorComputeNode Node;

      public EmitterGpu(VectorComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public List<NanoGpuBufferRef> gpuInputBuffers = new List<NanoGpuBufferRef>();
      public List<NanoGpuExternalBufferRef> gpuExternalInputBuffers = new List<NanoGpuExternalBufferRef>();
      public List<NanoGpuBufferRef> gpuOutputBuffers = new List<NanoGpuBufferRef>();
      public CodeCachedResult codeCachedResult;
      public string threadCountLocal;

      public override void EmitFunctionPreamble(out NanoFunction func) {
        // Begin generating the main results function.
        string[] functionModifiers = { "kernel" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, program.VoidType, functionModifiers);
        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        AddGpuFuncInputs(func, CollectComputeInputs(DependentComputeInputsToLoad), gpuInputBuffers, gpuExternalInputBuffers, ref bufferIndex);
        AddDebugGpuFuncInputs(func, gpuInputBuffers, ref bufferIndex);
        // Define outputs.
        // AddGpuFuncOutputs(func, computeOutputSpec.Fields, gpuOutputBuffers, ref bufferIndex);
        int outputIndex = 0;
        foreach (DataField field in computeOutputSpec.Fields) {
          if (field.IsCompileTimeOnly) {
            continue;
          }
          AddGpuFuncOutput(func, field, $"output{outputIndex}", gpuOutputBuffers, gpuExternalInputBuffers, ref bufferIndex, isAtomic: outputIndex == Node.AtomicCounterFieldIndex || outputIndex == Node.AtomicCounterFieldIndex2);
          outputIndex++;
        }

        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint), $"gid_uint", "[[thread_position_in_grid]]");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");
        func.AddStatement($"#if defined(DEBUG)");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Bool)} isDebugThread = gid == 0;");
        func.AddStatement($"#endif // defined(DEBUG)");
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        // Store results.
        this.codeCachedResult = new CodeCachedResult { ResultType = resultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec } };
        result = codeCachedResult;
        if (Node.DisableBufferWrite) {
          return;
        }

        switch (Node.OutputPortsMode) {
          case FieldPortsMode.Combined: {
            DataField outField = computeOutputSpec.Fields.First(field => field.Name == "Out");
            NanoProgramType outResultType = program.GetProgramType(outField.Type).ElementType;
            string returnLocal = func.AllocLocal("Return");
            func.AddStatement($"{func.GetTypeIdentifier(outResultType)} {returnLocal};");
            TypeField[] outputFields = Node.OutputTypeFields;
            foreach (var field in outputFields) {
              string inputExpr;
              switch (Node.InputPortsMode) {
                case FieldPortsMode.Combined: {
                  var edge = graph.GetEdgeToDestinationOrNull(computeNode, "Out");
                  if (edge == null) {
                    continue;
                  }
                  CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
                  if (inputLocal == null) {
                    NanoGraph.CurrentGenerateState.AddError($"Input {field.Name} for {computeNode} is not defined.");
                    continue;
                  }
                  NanoProgramType programInType = program.GetProgramType(inputLocal.Value.Type);
                  if (Node.IsArray) {
                    inputExpr = $"{inputLocal.Value.Identifier}";
                  } else {
                    inputExpr = $"{inputLocal.Value.Identifier}.{programInType.GetField(field.Name)}";
                  }
                  break;
                }
                default:
                case FieldPortsMode.Individual: {
                  var edge = graph.GetEdgeToDestinationOrNull(computeNode, field.Name);
                  if (edge == null) {
                    continue;
                  }
                  CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
                  if (inputLocal == null) {
                    NanoGraph.CurrentGenerateState.AddError($"Input {field.Name} for {computeNode} is not defined.");
                    continue;
                  }
                  inputExpr = inputLocal.Value.Identifier;
                  break;
                }
              }
              string outputExpr = $"{returnLocal}.{outResultType.GetField(field.Name)}";
              func.AddStatement($"{outputExpr} = {inputExpr};");
            }
            func.AddStatement($"{func.Context.EmitWriteBuffer($"output{0}", func.Context.EmitThreadId(), returnLocal)};");
            break;
          }
          default:
          case FieldPortsMode.Individual: {
            for (int i = 0; i < computeOutputSpec.Fields.Count; ++i) {
              var field = computeOutputSpec.Fields[i];
              if (field.IsCompileTimeOnly) {
                continue;
              }
              var edge = graph.GetEdgeToDestinationOrNull(computeNode, field.Name);
              if (edge == null) {
                continue;
              }
              CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
              if (inputLocal == null) {
                NanoGraph.CurrentGenerateState.AddError($"Input {field.Name} for {computeNode} is not defined.");
                continue;
              }
              if (field.Type.IsArray) {
                if (i == Node.AtomicCounterFieldIndex || i == Node.AtomicCounterFieldIndex2) {
                  string counterWriteIndexExpr = func.EmitConvert(inputLocal.Value.Type, TypeSpec.MakePrimitive(PrimitiveType.Int), inputLocal?.Identifier);
                  func.AddStatement($"atomic_fetch_add_explicit(&output{i}[{counterWriteIndexExpr}], 1, memory_order_relaxed);");
                } else {
                  func.AddStatement($"{func.Context.EmitWriteBuffer($"output{i}", func.Context.EmitThreadId(), inputLocal?.Identifier)};");
                }
              } else {
                func.AddStatement($"output{i} = {inputLocal?.Identifier};");
              }
            }
            break;
          }
        }
      }

      private string EmitThreadCountExpr() {
        string fieldName = Node.ThreadCountFields[0].Name;
        var edge = graph.GetEdgeToDestinationOrNull(computeNode, fieldName);
        if (edge == null) {
          return null;
        }
        if (!(edge.Source.Node is IComputeNode sourceComputeNode)) {
          NanoGraph.CurrentGenerateState.AddError($"Node {computeNode} depends on an output that is not a compute node ({edge.Source.Node}).");
          return null;
        }
        CodeCachedResult? sourceCachedResult = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == sourceComputeNode)?.Result;
        if (sourceCachedResult == null) {
          NanoGraph.CurrentGenerateState.AddError($"Node {computeNode} depends on a compute node that is not yet ready ({edge.Source.Node}).");
          return null;
        }

        string inputExpr = $"{sourceCachedResult?.Result.Identifier}.{sourceCachedResult?.ResultType.GetField(edge.Source.FieldName)}";
        switch (Node.ThreadCountMode) {
          case ThreadCountMode.Integer:
            return $"{inputExpr}";
          case ThreadCountMode.ArraySize:
          default:
            return $"GetLength({inputExpr})";
        }
      }

      public override void EmitValidateCacheFunctionInner() {
        string pipelineStateIdentifier = program.AddInstanceField(program.MTLComputePipelineStateType, $"{computeNode.ShortName}_GpuPipeline");

        // Sync buffers to GPU.
        string threadCountLocal = validateCacheFunction.AllocLocal("ThreadCount");
        validateCacheFunction.AddStatement($"{NanoProgram.IntIdentifier} {threadCountLocal} = {EmitThreadCountExpr()};");
        string threadCountExpr = threadCountLocal;
        string bufferSizeLocal = validateCacheFunction.AllocLocal("BufferSize");
        validateCacheFunction.AddStatement($"{NanoProgram.IntIdentifier} {bufferSizeLocal} = std::max({validateCacheFunction.EmitLiteral(Node.MinBufferSize)}, {threadCountExpr} * {validateCacheFunction.EmitLiteral(Node.BufferSizeMultiplier)});");
        int allocateBufferIndex = 0;
        foreach (var field in computeOutputSpec.Fields) {
          string thisBufferSizeExpr = bufferSizeLocal;
          if (allocateBufferIndex == Node.AtomicCounterFieldIndex) {
            thisBufferSizeExpr = validateCacheFunction.EmitLiteral(Node.AtomicCounterBufferLength);
          } else if (allocateBufferIndex == Node.AtomicCounterFieldIndex2) {
            thisBufferSizeExpr = validateCacheFunction.EmitLiteral(Node.AtomicCounterBufferLength2);
          }
          AllocateGpuFuncOutput(validateCacheFunction, field, thisBufferSizeExpr, gpuExternalInputBuffers);
          ++allocateBufferIndex;
        }
        foreach (var dependency in dependentComputeNodes.Where(dependency => dependency.Operation is ISplitComputeNodeEmitCodeOperation)) {
          var op = dependency.Operation as ISplitComputeNodeEmitCodeOperation;
          op?.EmitPreValidateCache(validateCacheFunction, Node, threadCountExpr);
        }
        EmitSyncBuffersToGpu(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        if (Node.AtomicCounterFieldIndex >= 0) {
          NanoProgramType outputType = func.Program.GetProgramType(cachedResult.Type);
          var outputBuffer = gpuOutputBuffers[Node.AtomicCounterFieldIndex];
          var fieldName = outputType.GetField(outputBuffer.FieldName);
          if (!outputBuffer.IsExternalBufferRef) {
            validateCacheFunction.AddStatement($"{{");
            validateCacheFunction.AddStatement($"  id<MTLBlitCommandEncoder> blitEncoder = [GetCurrentCommandBuffer() blitCommandEncoder];");
            validateCacheFunction.AddStatement($"  [blitEncoder fillBuffer:{cachedResult.Identifier}.{fieldName}->GetGpuBuffer() range:NSMakeRange(0, {cachedResult.Identifier}.{fieldName}->GetTotalByteLength()) value:0];");
            validateCacheFunction.AddStatement($"  [blitEncoder endEncoding];");
            validateCacheFunction.AddStatement($"}}");
          }
        }
        if (Node.AtomicCounterFieldIndex2 >= 0) {
          NanoProgramType outputType = func.Program.GetProgramType(cachedResult.Type);
          var outputBuffer = gpuOutputBuffers[Node.AtomicCounterFieldIndex2];
          var fieldName = outputType.GetField(outputBuffer.FieldName);
          validateCacheFunction.AddStatement($"{{");
          validateCacheFunction.AddStatement($"  id<MTLBlitCommandEncoder> blitEncoder = [GetCurrentCommandBuffer() blitCommandEncoder];");
          validateCacheFunction.AddStatement($"  [blitEncoder fillBuffer:{cachedResult.Identifier}.{fieldName}->GetGpuBuffer() range:NSMakeRange(0, {cachedResult.Identifier}.{fieldName}->GetTotalByteLength()) value:0];");
          validateCacheFunction.AddStatement($"  [blitEncoder endEncoding];");
          validateCacheFunction.AddStatement($"}}");
        }
        if (Node.ForceClearFieldIndex >= 0) {
          NanoProgramType outputType = func.Program.GetProgramType(cachedResult.Type);
          var outputBuffer = gpuOutputBuffers[Node.ForceClearFieldIndex];
          var fieldName = outputType.GetField(outputBuffer.FieldName);
          validateCacheFunction.AddStatement($"{{");
          validateCacheFunction.AddStatement($"  id<MTLBlitCommandEncoder> blitEncoder = [GetCurrentCommandBuffer() blitCommandEncoder];");
          validateCacheFunction.AddStatement($"  [blitEncoder fillBuffer:{cachedResult.Identifier}.{fieldName}->GetGpuBuffer() range:NSMakeRange(0, {cachedResult.Identifier}.{fieldName}->GetTotalByteLength()) value:0];");
          validateCacheFunction.AddStatement($"  [blitEncoder endEncoding];");
          validateCacheFunction.AddStatement($"}}");
        }

        validateCacheFunction.AddStatement($"id<MTLComputeCommandEncoder> encoder = [GetCurrentCommandBuffer() computeCommandEncoder];");
        validateCacheFunction.AddStatement($"[encoder setComputePipelineState:{pipelineStateIdentifier}];");
        // Bind buffers.
        EmitBindBuffers(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        // Run command queue.
        validateCacheFunction.AddStatement($"MTLSize batchSize = {{ (NSUInteger)({threadCountExpr}), 1, 1 }};");
        validateCacheFunction.AddStatement($"MTLSize threadgroupSize = {{ {pipelineStateIdentifier}.maxTotalThreadsPerThreadgroup, 1, 1 }};");
        validateCacheFunction.AddStatement($"[encoder dispatchThreads:batchSize threadsPerThreadgroup:threadgroupSize];");
        validateCacheFunction.AddStatement($"[encoder endEncoding];");
        EmitMarkBuffersDirty(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        // Emit pipeline creation code.
        createPipelinesFunction.AddStatement($"{pipelineStateIdentifier} = [device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@\"{func.Identifier}\"] error:&error];");
      }
    }
  }
}
