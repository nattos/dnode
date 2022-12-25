using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum TextureComputeSizeMode {
    Auto,
    PatchSize,
    Custom,
  }

  public enum TextureComputeSizeMultiplier {
    Full,
    Half,
    Quarter,
    OneEighth,
    Double,
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
    public TextureComputeSizeMultiplier SizeMultiplier = TextureComputeSizeMultiplier.Full;
    [EditableAttribute]
    public TextureComputeOutputMode OutputMode = TextureComputeOutputMode.TextureOnly;
    [EditableAttribute]
    // TODO: Support multiple output formats.
    public TextureChannelFormat Channels = TextureChannelFormat.RGBA;

    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;

    public override DataSpec InputSpec => DataSpec.FromFields(TextureSizeFields.Concat(InputFields).ToArray());
    public override DataSpec ComputeInputSpec => DataSpec.FromFields(InputFields);
    public override DataSpec OutputSpec => DataSpec.FromFields(new DataField { Name = "Out", Type = TypeSpec.MakePrimitive(PrimitiveType.Texture) });

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterGpu(this, context);
  
    public DataField[] InputFields => new[] { DataField.MakePrimitive("Color", PrimitiveType.Float4) };
    public DataField[] TextureSizeFields {
      get {
        switch (SizeMode) {
          case TextureComputeSizeMode.Auto:
          case TextureComputeSizeMode.PatchSize:
          default:
            return Array.Empty<DataField>();
          case TextureComputeSizeMode.Custom:
            return new[] { DataField.MakePrimitive("Size", PrimitiveType.Float2) };
        }
      }
    }

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
        }

        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        AddGpuFuncInputs(func, computeInputs, gpuInputBuffers, ref bufferIndex);
        AddDebugGpuFuncInputs(func, gpuInputBuffers, ref bufferIndex);
        AddGpuFuncOutputs(func, computeOutputSpec.Fields, gpuOutputBuffers, ref bufferIndex);
        if (graph.DebugEnabled) {
          AddGpuFuncInput(func, "debugOutputNodeIndex", program.IntType, "debugOutputNodeIndex", "debugOutputNodeIndex", gpuInputBuffers, ref bufferIndex, isReadWrite: false);
          AddGpuFuncOutput(func, "debugOutputTexture", program.Texture, "debugOutputTexture", "debugOutputTexture", gpuOutputBuffers, ref bufferIndex);
        }
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
        func.AddStatement($"WriteTexture({outputColorBuffer.ParameterName}, gid_xy_uint, {func.EmitConvert(outputColorLocal.Type, TypeSpec.MakePrimitive(PrimitiveType.Float4), outputColorLocal.Identifier)});");

        if (graph.DebugEnabled) {
          var outputDebugBuffer = gpuOutputBuffers.First(buffer => buffer.Expression == "debugOutputTexture");
          string debugOutLocal = func.AllocLocal("DebugOut");
          func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Float4)} {debugOutLocal} = 0.0;");
          func.AddStatement($"if (debugOutputNodeIndex >= 0) {{");
          func.AddStatement($"  switch (debugOutputNodeIndex) {{");
          int resultIndex = 0;
          foreach ((DataPlug dataPlug, CodeLocal resultLocal) in SortedResutLocalMap.Values) {
            func.AddStatement($"    case {func.EmitLiteral(resultIndex)}:");
            func.AddStatement($"      {debugOutLocal} = {func.EmitConvert(resultLocal.Type, TypeSpec.MakePrimitive(PrimitiveType.Float4), resultLocal.Identifier)};");
            func.AddStatement($"      break;");
            ++resultIndex;
          }
          func.AddStatement($"    case {func.EmitLiteral(resultIndex)}:");
          func.AddStatement($"      {debugOutLocal} = {func.EmitConvert(outputColorLocal.Type, TypeSpec.MakePrimitive(PrimitiveType.Float4), outputColorLocal.Identifier)};");
          func.AddStatement($"      break;");
          func.AddStatement($"  }}");
          func.AddStatement($"  WriteTexture({outputDebugBuffer.ParameterName}, gid_xy_uint, {debugOutLocal});");
          func.AddStatement($"}}");
        }

        result = codeCachedResult;
      }

      private string GetInputExpr(string fieldName) {
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
        return inputExpr;
      }

      private string EmitTextureSizeExpr(NanoFunction func) {
        switch (Node.SizeMode) {
          case TextureComputeSizeMode.Auto:
            return $"GetTextureSize({autoSizeModeInput?.Expression})";
          case TextureComputeSizeMode.PatchSize:
          default:
            return "OutputTextureSize";
          case TextureComputeSizeMode.Custom:
            return func.EmitConvert(func.Program.Float2Type, func.Program.Int2Type, GetInputExpr("Size"));
        }
      }

      public override void EmitValidateCacheFunctionInner() {
        string pipelineStateIdentifier = program.AddInstanceField(program.MTLComputePipelineStateType, $"{computeNode.ShortName}_GpuPipeline");

        int sizeNumerator;
        int sizeDenominator;
        switch (Node.SizeMultiplier) {
          case TextureComputeSizeMultiplier.Full:
          default:
            sizeNumerator = 1;
            sizeDenominator = 1;
            break;
          case TextureComputeSizeMultiplier.Half:
            sizeNumerator = 1;
            sizeDenominator = 2;
            break;
          case TextureComputeSizeMultiplier.Quarter:
            sizeNumerator = 1;
            sizeDenominator = 4;
            break;
          case TextureComputeSizeMultiplier.OneEighth:
            sizeNumerator = 1;
            sizeDenominator = 8;
            break;
          case TextureComputeSizeMultiplier.Double:
            sizeNumerator = 2;
            sizeDenominator = 1;
            break;
        }

        string gridSizeLocal = validateCacheFunction.AllocLocal("Size");
        validateCacheFunction.AddStatement($"{validateCacheFunction.Program.Int2Type.Identifier} {gridSizeLocal} = {EmitTextureSizeExpr(validateCacheFunction)};");
        string gridSizeExpr = gridSizeLocal;
        string gridSizeXLocal = validateCacheFunction.AllocLocal("SizeX");
        string gridSizeYLocal = validateCacheFunction.AllocLocal("SizeY");
        validateCacheFunction.AddStatement($"{validateCacheFunction.Program.IntType.Identifier} {gridSizeXLocal} = (int)(({gridSizeExpr}).x * {sizeNumerator} / {sizeDenominator});");
        validateCacheFunction.AddStatement($"{validateCacheFunction.Program.IntType.Identifier} {gridSizeYLocal} = (int)(({gridSizeExpr}).y * {sizeNumerator} / {sizeDenominator});");
        string gridSizeXExpr = gridSizeXLocal;
        string gridSizeYExpr = gridSizeYLocal;
        string totalThreadCountExpr = $"(({gridSizeXExpr}) * ({gridSizeYExpr}))";

        if (graph.DebugEnabled) {
          validateCacheFunction.AddStatement($"const std::string& debugOutputTextureKey = DebugGetOutputTextureKey();");
          validateCacheFunction.AddStatement($"int debugOutputNodeIndex = -1;");
          {
            bool first = true;
            int resultIndex = 0;
            foreach ((DataPlug dataPlug, CodeLocal resultLocal) in SortedResutLocalMap.Values) {
              string ifBranch = first ? "if (" : "} else if (";
              first = false;
              string thisKey = $"{dataPlug.Node.DebugId}.{dataPlug.FieldName}";
              validateCacheFunction.AddStatement($"{ifBranch}debugOutputTextureKey == {validateCacheFunction.EmitLiteral(thisKey)}) {{");
              validateCacheFunction.AddStatement($"  debugOutputNodeIndex = {resultIndex};");
              ++resultIndex;
            }
            {
              string ifBranch = first ? "if (" : "} else if (";
              first = false;
              string thisKey = $"{Node.DebugId}.Out";
              validateCacheFunction.AddStatement($"{ifBranch}debugOutputTextureKey == {validateCacheFunction.EmitLiteral(thisKey)}) {{");
              validateCacheFunction.AddStatement($"  debugOutputNodeIndex = {resultIndex};");
            }
            validateCacheFunction.AddStatement($"}}");
          }

          validateCacheFunction.AddStatement($"ResizeSharedTexture(_debugOutputTexture, GetDevice(), {gridSizeXExpr}, {gridSizeYExpr});");
          validateCacheFunction.AddStatement($"id<MTLTexture> debugOutputTexture = debugOutputNodeIndex >= 0 ? _debugOutputTexture->Texture : nullptr;");
        }

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
