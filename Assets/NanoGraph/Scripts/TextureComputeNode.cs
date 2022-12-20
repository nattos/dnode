using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum TextureComputeSizeMode {
    Auto,
    Custom,
  }

  public enum TextureComputeOutputMode {
    TextureOnly,
    DataOnly,
    TextureAndData,
  }

  public enum TextureChannelFormat {
    A,
    RGBA,
  }

  public class TextureComputeNode : ComputeNode {
    // TODO: Support multiple size modes.
    [EditableAttribute]
    public TextureComputeSizeMode SizeMode = TextureComputeSizeMode.Auto;
    // TODO: Support multiple output modes.
    [EditableAttribute]
    public TextureComputeOutputMode OutputMode = TextureComputeOutputMode.TextureOnly;
    [EditableAttribute]
    // TODO: Support multiple output formats.
    public TextureChannelFormat Channels = TextureChannelFormat.RGBA;

    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakePrimitive("Color", PrimitiveType.Float4));
    public override DataSpec OutputSpec => DataSpec.FromFields(new DataField { Name = "Out", Type = TypeSpec.MakePrimitive(PrimitiveType.Texture) });

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterGpu(this, context);
  
    private class EmitterGpu : EmitterBase {
      public readonly TextureComputeNode Node;

      public EmitterGpu(TextureComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public List<NanoGpuBufferRef> gpuInputBuffers = new List<NanoGpuBufferRef>();
      public List<NanoGpuBufferRef> gpuOutputBuffers = new List<NanoGpuBufferRef>();
      public CodeCachedResult codeCachedResult;
      private ComputeInput? autoSizeModeInput = null;

      public override void EmitFunctionPreamble(out NanoFunction func) {
        // Begin generating the main results function.
        string[] functionModifiers = { "kernel" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, program.VoidType, functionModifiers);

        // Determine which input texture to base grid size.
        ComputeInput[] computeInputs = CollectComputeInputs(DependentComputeInputsToLoad).ToArray();
        switch (Node.SizeMode) {
          default:
          case TextureComputeSizeMode.Auto: {
            foreach (var computeInput in computeInputs) {
              if (computeInput.Field.Type.Primitive == PrimitiveType.Texture) {
                autoSizeModeInput = computeInput;
                break;
              }
            }
            if (autoSizeModeInput == null) {
              errors.Add($"Node {Node} is using auto-size mode but does not have a texture input.");
            }
            break;
          }
          case TextureComputeSizeMode.Custom:
            // TODO.
            throw new NotImplementedException();
        }

        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        AddGpuFuncInputs(func, computeInputs, gpuInputBuffers, ref bufferIndex);
        AddGpuFuncOutputs(func, computeOutputSpec.Fields, gpuOutputBuffers, ref bufferIndex);
        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint2), $"gid_xy_uint", "[[thread_position_in_grid]]");
        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint2), $"size_xy_uint", "[[threads_per_grid]]");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Uint)} gid_uint = gid_xy_uint.x + (gid_xy_uint.y * size_xy_uint.x);");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");
        func.AddStatement($"vector_int2 gid_xy = vector_int2(gid_xy_uint);");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Float2)} gid_xy_norm = float2(gid_xy_uint) / float2(size_xy_uint);");
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        // Store results.
        this.codeCachedResult = new CodeCachedResult { ResultType = resultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec } };

        var outputColorSourceEdge = graph.GetEdgeToDestinationOrNull(new DataPlug { Node = Node, FieldName = "Color" });
        if (outputColorSourceEdge == null ||
            !resultLocalMap.TryGetValue(outputColorSourceEdge.Source, out CodeLocal outputColorLocal)) {
          errors.Add($"Output color for {Node} is not set.");
          result = null;
          return;
        }
        var outputColorBuffer = gpuOutputBuffers.First(buffer => buffer.FieldName == "Out");
        func.AddStatement($"WriteTexture({outputColorBuffer.Expression}, gid_xy_uint, {outputColorLocal.Identifier});");
        result = codeCachedResult;
      }

      public override void EmitValidateCacheFunction() {
        validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, program.VoidType);
        string pipelineStateIdentifier = program.AddInstanceField(program.MTLComputePipelineStateType, $"{computeNode.ShortName}_GpuPipeline");

        string gridSizeExpr = null;
        switch (Node.SizeMode) {
          default:
          case TextureComputeSizeMode.Auto: {
            if (autoSizeModeInput != null) {
              gridSizeExpr = $"GetTextureSize({autoSizeModeInput?.Expression})";
            }
            break;
          }
          case TextureComputeSizeMode.Custom:
            // TODO.
            throw new NotImplementedException();
        }
        string gridSizeXExpr = $"({gridSizeExpr}).x";
        string gridSizeYExpr = $"({gridSizeExpr}).y";
        string totalThreadCountExpr = $"(({gridSizeXExpr}) * ({gridSizeYExpr}))";

        foreach (var dependency in dependentComputeNodes.Where(dependency => dependency.Operation is ISplitComputeNodeEmitCodeOperation)) {
          var op = dependency.Operation as ISplitComputeNodeEmitCodeOperation;
          op?.EmitPreValidateCache(validateCacheFunction, Node, totalThreadCountExpr);
        }

        // Sync buffers to GPU.
        EmitSyncBuffersToGpu(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        string outIdentifier = $"{codeCachedResult.Result.Identifier}.{codeCachedResult.ResultType.GetField("Out")}";
        validateCacheFunction.AddStatement($"{outIdentifier} = ResizeTexture({outIdentifier}, {gridSizeXExpr}, {gridSizeYExpr});");

        validateCacheFunction.AddStatement($"id<MTLComputeCommandEncoder> encoder = [GetCurrentCommandBuffer() computeCommandEncoder];");
        validateCacheFunction.AddStatement($"[encoder setComputePipelineState:{pipelineStateIdentifier}];");

        // Bind buffers.
        EmitBindBuffers(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        // Run command queue.
        validateCacheFunction.AddStatement($"MTLSize batchSize = {{ (NSUInteger)({gridSizeXExpr}), (NSUInteger)({gridSizeYExpr}), 1 }};");
        validateCacheFunction.AddStatement($"MTLSize threadgroupSize = {{ {pipelineStateIdentifier}.maxTotalThreadsPerThreadgroup, 1, 1 }};");
        validateCacheFunction.AddStatement($"[encoder dispatchThreads:batchSize threadsPerThreadgroup:threadgroupSize];");
        validateCacheFunction.AddStatement($"[encoder endEncoding];");
        EmitMarkBuffersDirty(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        foreach (var dependency in dependentComputeNodes.Where(dependency => dependency.Operation is ISplitComputeNodeEmitCodeOperation)) {
          var op = dependency.Operation as ISplitComputeNodeEmitCodeOperation;
          op?.EmitPostValidateCache(validateCacheFunction, Node);
        }

        // Emit pipeline creation code.
        createPipelinesFunction.AddStatement($"{pipelineStateIdentifier} = [device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@\"{func.Identifier}\"] error:&error];");
      }
    }
  }
}
