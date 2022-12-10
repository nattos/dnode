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

    public override DataSpec AuxSizesOutputSpec => DataSpec.FromFields(DataField.MakePrimitive("GridSizeX", PrimitiveType.Uint), DataField.MakePrimitive("GridSizeY", PrimitiveType.Uint));

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

      public override void EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc) {
        // Begin generating the main results function.
        string[] functionModifiers = { "kernel" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, program.VoidType, functionModifiers);
        this.arraySizesFunc = arraySizesFunc = program.AddFunction($"{computeNode.ShortName}_Sizes", NanoProgram.CpuContext, arraySizeResultType);


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
        string returnSizesLocal = arraySizesFunc.AllocLocal("Return");
        this.codeCachedResult = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec, ArraySizeIdentifier = cachedResult.ArraySizeIdentifier } };
        arraySizesFunc.AddStatement($"{arraySizesFunc.GetTypeIdentifier(arraySizeResultType)} {returnSizesLocal};");

        var outputColorSourceEdge = graph.GetEdgeToDestinationOrNull(new DataPlug { Node = Node, FieldName = "Color" });
        if (outputColorSourceEdge == null ||
            !resultLocalMap.TryGetValue(outputColorSourceEdge.Source, out CodeLocal outputColorLocal)) {
          errors.Add($"Output color for {Node} is not set.");
          result = null;
          return;
        }
        var outputColorBuffer = gpuOutputBuffers.First(buffer => buffer.FieldName == "Out");
        func.AddStatement($"WriteTexture({outputColorBuffer.Expression}, gid_xy_uint, {outputColorLocal.Identifier});");

        // var auxSizesOutputSpec = computeNode.AuxSizesOutputSpec;
        // CodeLocal[] auxSizesCodeInputLocals = new CodeLocal[auxSizesOutputSpec.Fields.Count];
        // for (int i = 0; i < auxSizesOutputSpec.Fields.Count; ++i) {
        //   var field = auxSizesOutputSpec.Fields[i];
        //   if (field.IsCompileTimeOnly) {
        //     continue;
        //   }
        //   var edge = graph.GetEdgeToDestinationOrNull(computeNode, field.Name);
        //   if (edge == null) {
        //     continue;
        //   }
        //   if (!(edge.Source.Node is IComputeNode sourceComputeNode)) {
        //     errors.Add($"Node {computeNode} depends on an output that is not a compute node ({edge.Source.Node}).");
        //     continue;
        //   }
        //   CodeCachedResult? sourceCachedResult = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == sourceComputeNode)?.Result;
        //   if (sourceCachedResult == null) {
        //     errors.Add($"Node {computeNode} depends on a compute node that is not yet ready ({edge.Source.Node}).");
        //     continue;
        //   }
        //   string inputLocal = $"{sourceCachedResult?.Result.Identifier}.{sourceCachedResult?.ArraySizesResultType.GetField(edge.Source.FieldName)}";
        //   string inputSizeLocal = resultLocalMap.GetOrNull(edge.Source)?.ArraySizeIdentifier;
        //   if (inputLocal == null || inputSizeLocal == null) {
        //     errors.Add($"Input {field.Name} for {computeNode} is not defined.");
        //     continue;
        //   }
        //   auxSizesCodeInputLocals[i] = new CodeLocal { Identifier = inputLocal, Type = sourceCachedResult.Value.Result.Type, ArraySizeIdentifier = inputSizeLocal };
        // }

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
        string gridSizeLocal = arraySizesFunc.AllocLocal("GridSize");
        arraySizesFunc.AddStatement($"{program.Uint2Type.Identifier} {gridSizeLocal} = ({program.Uint2Type.Identifier}){gridSizeExpr};");
        arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField("GridSizeX")} = {gridSizeLocal}.x;");
        arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField("GridSizeY")} = {gridSizeLocal}.y;");

        // // TODO: Map inputs to outputs somehow.
        // for (int i = 0; i < computeOutputSpec.Fields.Count; ++i) {
        //   var field = computeOutputSpec.Fields[i];
        //   if (field.IsCompileTimeOnly) {
        //     continue;
        //   }
        //   var edge = graph.GetEdgeToDestinationOrNull(computeNode, field.Name);
        //   if (edge == null) {
        //     continue;
        //   }
        //   CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
        //   if (inputLocal == null) {
        //     errors.Add($"Input {field.Name} for {computeNode} is not defined.");
        //     continue;
        //   }
        //   if (field.Type.IsArray) {
        //     func.AddStatement($"{func.Context.EmitWriteBuffer($"output{i}", func.Context.EmitThreadId(), inputLocal?.Identifier)};");
        //   } else {
        //     func.AddStatement($"output{i} = {inputLocal?.Identifier};");
        //   }
        //   // arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {inputSizeLocal};");
        //   // arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {"TODO"};");
        // }
        arraySizesFunc.AddStatement($"return {returnSizesLocal};");
        result = codeCachedResult;
      }

      public override void EmitValidateCacheFunction() {
        validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, program.VoidType);
        validateCacheFunction.AddStatement($"{validateSizesCacheFunction.Identifier}();");
        string pipelineStateIdentifier = program.AddInstanceField(program.MTLComputePipelineStateType, $"{computeNode.ShortName}_GpuPipeline");

        // Sync buffers to GPU.
        EmitSyncBuffersToGpu(validateCacheFunction, cachedResult, gpuInputBuffers, gpuOutputBuffers);

        string gridSizeXExpr = $"{codeCachedResult.Result.ArraySizeIdentifier}.{codeCachedResult.ArraySizesResultType.GetField("GridSizeX")}";
        string gridSizeYExpr = $"{codeCachedResult.Result.ArraySizeIdentifier}.{codeCachedResult.ArraySizesResultType.GetField("GridSizeY")}";
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

        // Emit pipeline creation code.
        createPipelinesFunction.AddStatement($"{pipelineStateIdentifier} = [device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@\"{func.Identifier}\"] error:&error];");
      }
    }
  }
}
