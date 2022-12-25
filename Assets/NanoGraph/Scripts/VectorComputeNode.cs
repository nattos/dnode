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

    public override DataSpec ComputeInputSpec => base.ComputeInputSpec;
    public override DataSpec InputSpec => DataSpec.ExtendWithFields(ThreadCountFields, base.InputSpec);

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
      public string threadCountLocal;

      public override void EmitFunctionPreamble(out NanoFunction func) {
        // Begin generating the main results function.
        string[] functionModifiers = { "kernel" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, program.VoidType, functionModifiers);
        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        AddGpuFuncInputs(func, CollectComputeInputs(DependentComputeInputsToLoad), gpuInputBuffers, ref bufferIndex);
        AddDebugGpuFuncInputs(func, gpuInputBuffers, ref bufferIndex);
        // Define outputs.
        AddGpuFuncOutputs(func, computeOutputSpec.Fields, gpuOutputBuffers, ref bufferIndex);
        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint), $"gid_uint", "[[thread_position_in_grid]]");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        // Store results.
        this.codeCachedResult = new CodeCachedResult { ResultType = resultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec } };

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
        }
        result = codeCachedResult;
      }

      private string EmitThreadCountExpr() {
        string fieldName = Node.ThreadCountFields[0].Name;
        var edge = graph.GetEdgeToDestinationOrNull(computeNode, fieldName);
        if (edge == null) {
          return null;
        }
        if (!(edge.Source.Node is IComputeNode sourceComputeNode)) {
          errors.Add($"Node {computeNode} depends on an output that is not a compute node ({edge.Source.Node}).");
          return null;
        }
        CodeCachedResult? sourceCachedResult = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == sourceComputeNode)?.Result;
        if (sourceCachedResult == null) {
          errors.Add($"Node {computeNode} depends on a compute node that is not yet ready ({edge.Source.Node}).");
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
        EmitSyncBuffersToGpu(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);
        validateCacheFunction.AddStatement($"id<MTLComputeCommandEncoder> encoder = [GetCurrentCommandBuffer() computeCommandEncoder];");
        validateCacheFunction.AddStatement($"[encoder setComputePipelineState:{pipelineStateIdentifier}];");
        // Bind buffers.
        EmitBindBuffers(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        // Run command queue.
        validateCacheFunction.AddStatement($"MTLSize batchSize = {{ (NSUInteger)({EmitThreadCountExpr()}), 1, 1 }};");
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
