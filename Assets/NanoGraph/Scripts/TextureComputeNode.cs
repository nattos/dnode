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

  public enum GlobalTextureBitDepth {
    Int8,
    Float16,
    Float32,
  }

  public enum TextureBitDepth {
    Global,
    Int8,
    Float16,
    Float32,
  }

  public class TextureComputeNode : ComputeNode {
    [EditableAttribute]
    public TextureComputeSizeMode SizeMode = TextureComputeSizeMode.Auto;
    [EditableAttribute]
    public TextureComputeSizeMultiplier SizeMultiplier = TextureComputeSizeMultiplier.Full;
    // TODO: Support multiple output modes.
    [EditableAttribute]
    public TextureComputeOutputMode OutputMode = TextureComputeOutputMode.TextureOnly;
    [EditableAttribute]
    public TextureChannelFormat Channels = TextureChannelFormat.RGBA;
    [EditableAttribute]
    public TextureBitDepth BitDepth = TextureBitDepth.Global;

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
          case TextureComputeSizeMode.Custom:
          case TextureComputeSizeMode.PatchSize:
            break;
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
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Float2)} gid_xy_norm = (float2(gid_xy_uint) + 0.5) / float2(size_xy_uint);");
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

          string[] GetExtractDebugValueExprs(string inputExpr, TypeSpec type, int limit) {
            if (type.IsArray) {
              List<string> resultExprs = new List<string>();
              int arrayIndex = 0;
              while (resultExprs.Count < limit) {
                string elementInputExpr = func.Context.EmitSampleBuffer(inputExpr, func.EmitLiteral(arrayIndex));
                string[] elementExprs = GetExtractDebugValueExprs(elementInputExpr, type.ElementSpec, limit);
                if (elementExprs.Length == 0) {
                  break;
                }
                foreach (string elementExpr in elementExprs) {
                  resultExprs.Add($"({arrayIndex} < GetLength({inputExpr}) ? ({elementExpr}) : (0.0))");
                }
                ++arrayIndex;
              }
              return resultExprs.ToArray();
            }
            if (type.Primitive != null) {
              switch (type.Primitive.Value) {
                case PrimitiveType.Bool:
                  return new[] { $"(({inputExpr}) ? 1.0 : 0.0)" };
                case PrimitiveType.Int:
                  return new[] { $"((float)({inputExpr}))" };
                case PrimitiveType.Uint:
                  return new[] { $"((float)({inputExpr}))" };
                case PrimitiveType.Uint2:
                  return new[] { $"((float)(({inputExpr}).x))", $"((float)(({inputExpr}).y))" };
                case PrimitiveType.Double:
                  return new[] { $"((float)({inputExpr}))" };
                case PrimitiveType.Float:
                  return new[] { $"((float)({inputExpr}))" };
                case PrimitiveType.Float2:
                  return new[] { $"((float)(({inputExpr}).x))", $"((float)(({inputExpr}).y))" };
                case PrimitiveType.Float3:
                  return new[] { $"((float)(({inputExpr}).x))", $"((float)(({inputExpr}).y))", $"((float)(({inputExpr}).z))" };
                case PrimitiveType.Float4:
                  return new[] { $"((float)(({inputExpr}).x))", $"((float)(({inputExpr}).y))", $"((float)(({inputExpr}).z))", $"((float)(({inputExpr}).w))" };
                default:
                  return Array.Empty<string>();
              }
            }
            if (type.Type != null) {
              NanoProgramType programType = program.GetProgramType(TypeSpec.MakeType(type.Type));
              return type.Type.Fields.SelectMany(field => {
                string fieldInputExpr = $"{inputExpr}.{programType.GetField(field.Name)}";
                return GetExtractDebugValueExprs(fieldInputExpr, field.Type, limit);
              }).ToArray();
            }
            return Array.Empty<string>();
          }

          foreach ((DataPlug dataPlug, CodeLocal resultLocal) in SortedResutLocalMap.Values) {
            string extractDebugValueExpr;
            string[] valueExprs = GetExtractDebugValueExprs(resultLocal.Identifier, resultLocal.Type, limit: 4);
            if (valueExprs.Length == 1) {
              extractDebugValueExpr = $"vector_float4 {{ {valueExprs[0]}, {valueExprs[0]}, {valueExprs[0]}, 1.0 }}";
            } else if (valueExprs.Length > 0) {
              extractDebugValueExpr = $"vector_float4 {{ {string.Join(", ", valueExprs.Take(4))} }}";
            } else {
              extractDebugValueExpr = $"0.0";
            }

            func.AddStatement($"    case {func.EmitLiteral(resultIndex)}:");
            func.AddStatement($"      {debugOutLocal} = {extractDebugValueExpr};");
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
        validateCacheFunction.AddStatement($"{outIdentifier} = ResizeTexture({outIdentifier}, {gridSizeXExpr}, {gridSizeYExpr}, {NanoGpuContext.BitDepthToMetal(graph.GetTextureBitDepth(Node.BitDepth))});");

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
