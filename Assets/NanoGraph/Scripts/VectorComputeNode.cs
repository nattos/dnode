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
    public override DataSpec AuxSizesOutputSpec => DataSpec.FromFields(ThreadCountFields);

    public override DataSpec ComputeInputSpec => base.ComputeInputSpec;
    public override DataSpec InputSpec => DataSpec.ExtendWithFields(base.InputSpec, ThreadCountFields);

    public override DataSpec OutputSpec {
      get {
        return DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(TypeSpec.MakeType(new TypeDecl(OutputTypeFields.ToArray())))));
      }
    }

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
      public List<NanoGpuBufferRef> gpuOutputBuffers = new List<NanoGpuBufferRef>();
      public CodeCachedResult codeCachedResult;

      public override void EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc) {
        // Begin generating the main results function.
        string[] functionModifiers = { "kernel" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, program.VoidType, functionModifiers);
        this.arraySizesFunc = arraySizesFunc = program.AddFunction($"{computeNode.ShortName}_Sizes", NanoProgram.CpuContext, arraySizeResultType);
        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        AddGpuFuncInputs(func, CollectComputeInputs(DependentComputeInputsToLoad), gpuInputBuffers, ref bufferIndex);
        // Define outputs.
        AddGpuFuncOutputs(func, computeOutputSpec.Fields, gpuOutputBuffers, ref bufferIndex);
        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint), $"gid_uint", "[[thread_position_in_grid]]");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        // Store results.
        string returnSizesLocal = arraySizesFunc.AllocLocal("Return");
        this.codeCachedResult = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec, ArraySizeIdentifier = cachedResult.ArraySizeIdentifier } };
        arraySizesFunc.AddStatement($"{arraySizesFunc.GetTypeIdentifier(arraySizeResultType)} {returnSizesLocal};");

        var auxSizesOutputSpec = computeNode.AuxSizesOutputSpec;
        CodeLocal[] auxSizesCodeInputLocals = new CodeLocal[auxSizesOutputSpec.Fields.Count];
        for (int i = 0; i < auxSizesOutputSpec.Fields.Count; ++i) {
          var field = auxSizesOutputSpec.Fields[i];
          if (field.IsCompileTimeOnly) {
            continue;
          }
          var edge = graph.GetEdgeToDestinationOrNull(computeNode, field.Name);
          if (edge == null) {
            continue;
          }
          if (!(edge.Source.Node is IComputeNode sourceComputeNode)) {
            errors.Add($"Node {computeNode} depends on an output that is not a compute node ({edge.Source.Node}).");
            continue;
          }
          CodeCachedResult? sourceCachedResult = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == sourceComputeNode)?.Result;
          if (sourceCachedResult == null) {
            errors.Add($"Node {computeNode} depends on a compute node that is not yet ready ({edge.Source.Node}).");
            continue;
          }
          string inputLocal = $"{sourceCachedResult?.Result.Identifier}.{sourceCachedResult?.ArraySizesResultType.GetField(edge.Source.FieldName)}";
          string inputSizeLocal = resultLocalMap.GetOrNull(edge.Source)?.ArraySizeIdentifier;
          if (inputLocal == null || inputSizeLocal == null) {
            errors.Add($"Input {field.Name} for {computeNode} is not defined.");
            continue;
          }
          auxSizesCodeInputLocals[i] = new CodeLocal { Identifier = inputLocal, Type = sourceCachedResult.Value.Result.Type, ArraySizeIdentifier = inputSizeLocal };
        }
        var emitStoreAuxSizesCodeCachedResult = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { ArraySizeIdentifier = returnSizesLocal } };
        EmitStoreAuxSizesCode(new CodeContext {
          Function = func,
          ArraySizeFunction = arraySizesFunc,
          InputLocals = auxSizesCodeInputLocals,
          OutputLocals = null,
          Errors = errors,
        }, emitStoreAuxSizesCodeCachedResult);

        // TODO: Map inputs to outputs somehow.
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
            errors.Add($"Input {field.Name} for {computeNode} is not defined.");
            continue;
          }
          if (field.Type.IsArray) {
            func.AddStatement($"{func.Context.EmitWriteBuffer($"output{i}", func.Context.EmitThreadId(), inputLocal?.Identifier)};");
          } else {
            func.AddStatement($"output{i} = {inputLocal?.Identifier};");
          }
          // arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {inputSizeLocal};");
          arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {EmitTotalThreadCount(arraySizesFunc)};");
        }
        arraySizesFunc.AddStatement($"return {returnSizesLocal};");
        result = codeCachedResult;
      }

      private string EmitTotalThreadCount(NanoFunction func) {
        switch (Node.ThreadCountMode) {
          case ThreadCountMode.Integer:
            return $"{codeCachedResult.Result.ArraySizeIdentifier}.{codeCachedResult.ArraySizesResultType.GetField("ThreadCount")}";
          case ThreadCountMode.ArraySize:
          default:
            return $"{codeCachedResult.Result.ArraySizeIdentifier}.{codeCachedResult.ArraySizesResultType.GetField("ThreadCountFromArray")}";
        }
      }

      private void EmitStoreAuxSizesCode(CodeContext context, CodeCachedResult cachedResult) {
        string threadCountFieldName;
        string threadCountExpr;
        switch (Node.ThreadCountMode) {
          case ThreadCountMode.Integer:
            threadCountFieldName = "ThreadCount";
            threadCountExpr = $"{context.InputLocals[0].Identifier}";
            break;
          case ThreadCountMode.ArraySize:
          default:
            threadCountFieldName = "ThreadCountFromArray";
            threadCountExpr = $"{context.InputLocals[0].ArraySizeIdentifier}";
            break;
        }
        context.ArraySizeFunction.AddStatement($"{cachedResult.Result.ArraySizeIdentifier}.{cachedResult.ArraySizesResultType.GetField(threadCountFieldName)} = {threadCountExpr};");
      }

      public override void EmitValidateCacheFunction() {
        validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, program.VoidType);
        validateCacheFunction.AddStatement($"{validateSizesCacheFunction.Identifier}();");
        string pipelineStateIdentifier = program.AddInstanceField(program.MTLComputePipelineStateType, $"{computeNode.ShortName}_GpuPipeline");

        // Sync buffers to GPU.
        EmitSyncBuffersToGpu(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);
        // foreach (var inputBuffer in gpuInputBuffers) {
        //   if (inputBuffer.Type.IsArray) {
        //     validateCacheFunction.AddStatement($"{inputBuffer.Expression}->SyncToGpu();");
        //   }
        // }
        // foreach (var outputBuffer in gpuOutputBuffers) {
        //   var fieldName = resultType.GetField(outputBuffer.FieldName);
        //   if (outputBuffer.Type.IsArray) {
        //     validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName}->EnsureGpuBuffer();");
        //   }
        // }
        validateCacheFunction.AddStatement($"id<MTLComputeCommandEncoder> encoder = [GetCurrentCommandBuffer() computeCommandEncoder];");
        validateCacheFunction.AddStatement($"[encoder setComputePipelineState:{pipelineStateIdentifier}];");
        // Bind buffers.
        EmitBindBuffers(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);
        // foreach (var inputBuffer in gpuInputBuffers) {
        //   string expression = inputBuffer.Expression;
        //   int bufferIndex = inputBuffer.Index;
        //   if (inputBuffer.Type.IsArray) {
        //     validateCacheFunction.AddStatement($"[encoder setBuffer:{expression}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
        //   } else {
        //     validateCacheFunction.AddStatement($"[encoder setBytes:&{expression} length:sizeof({expression}) atIndex:{bufferIndex}];");
        //   }
        // }
        // foreach (var outputBuffer in gpuOutputBuffers) {
        //   var fieldName = resultType.GetField(outputBuffer.FieldName);
        //   int bufferIndex = outputBuffer.Index;
        //   if (outputBuffer.Type.IsArray) {
        //     validateCacheFunction.AddStatement($"[encoder setBuffer:{cachedResult.Identifier}.{fieldName}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
        //   }
        // }
        // Run command queue.
        validateCacheFunction.AddStatement($"MTLSize batchSize = {{ (NSUInteger)({EmitTotalThreadCount(validateCacheFunction)}), 1, 1 }};");
        validateCacheFunction.AddStatement($"MTLSize threadgroupSize = {{ {pipelineStateIdentifier}.maxTotalThreadsPerThreadgroup, 1, 1 }};");
        validateCacheFunction.AddStatement($"[encoder dispatchThreads:batchSize threadsPerThreadgroup:threadgroupSize];");
        validateCacheFunction.AddStatement($"[encoder endEncoding];");
        EmitMarkBuffersDirty(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);
        // foreach (var outputBuffer in gpuOutputBuffers) {
        //   var fieldName = resultType.GetField(outputBuffer.FieldName);
        //   if (outputBuffer.Type.IsArray) {
        //     validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName}->MarkGpuBufferChanged();");
        //   }
        // }

        // Emit pipeline creation code.
        createPipelinesFunction.AddStatement($"{pipelineStateIdentifier} = [device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@\"{func.Identifier}\"] error:&error];");
      }
    }
  }
}
