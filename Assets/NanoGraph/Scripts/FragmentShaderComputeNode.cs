using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum BlendOperation {
    Add,
    Subtract,
    ReverseSubtract,
    Min,
    Max,
  }

  public enum BlendFactor {
    Zero,
    One,
    SourceColor,
    OneMinusSourceColor,
    SourceAlpha,
    OneMinusSourceAlpha,
    DestinationColor,
    OneMinusDestinationColor,
    DestinationAlpha,
    OneMinusDestinationAlpha,
  }

  // A fragment shader takes the outputs of a vertex shader and turns that into a color.
  //
  // Its color input must depend on the outputs of exactly one vertex shader node, although it may
  // depend on any number of other scalar or vector compute nodes. It always outputs a texture.
  public class FragmentShaderComputeNode : ComputeNode {
    [EditableAttribute]
    public TextureComputeSizeMode SizeMode = TextureComputeSizeMode.Auto;
    [EditableAttribute]
    public TextureComputeSizeMultiplier SizeMultiplier = TextureComputeSizeMultiplier.Full;
    [EditableAttribute]
    public TextureChannelFormat Channels = TextureChannelFormat.RGBA;
    [EditableAttribute]
    public TextureBitDepth BitDepth = TextureBitDepth.Global;

    [EditableAttribute]
    public bool BlendEnabled = false;
    [EditableAttribute]
    public BlendOperation ColorBlendOperation = BlendOperation.Add;
    [EditableAttribute]
    public BlendOperation AlphaBlendOperation = BlendOperation.Add;
    [EditableAttribute]
    public BlendFactor SourceColorBlendFactor = BlendFactor.One;
    [EditableAttribute]
    public BlendFactor SourceAlphaBlendFactor = BlendFactor.One;
    [EditableAttribute]
    public BlendFactor DestinationColorBlendFactor = BlendFactor.Zero;
    [EditableAttribute]
    public BlendFactor DestinationAlphaBlendFactor = BlendFactor.Zero;

    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;

    public override DataSpec InputSpec => DataSpec.FromFields(new[] { new DataField { Name = "Verts", Type = TypeSpec.MakePrimitive(PrimitiveType.Vertices), IsCompileTimeOnly = true } }.Concat(TextureSizeFields).Append(DataField.MakePrimitive("Color", PrimitiveType.Float4)).ToArray());
    public override DataSpec OutputSpec => DataSpec.FromFields(new DataField { Name = "Out", Type = TypeSpec.MakePrimitive(PrimitiveType.Texture) });

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

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterGpu(this, context);
  
    public class EmitterGpu : EmitterBase {
      public readonly FragmentShaderComputeNode Node;

      public EmitterGpu(FragmentShaderComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public VertexShaderComputeNode vertexNode { get; private set; }
      public VertexShaderComputeNode.EmitterGpu vertexOp { get; private set; }
      public TypeDecl vertexType;
      public NanoProgramType vertexProgramType;
      public List<NanoGpuBufferRef> gpuInputBuffers = new List<NanoGpuBufferRef>();

      protected override IEnumerable<DataPlug> DependentComputeInputsToLoad => dependentComputeInputs.Where(plug => !(plug.Node == vertexNode && plug.FieldName == "Verts"));

      public override void EmitFunctionPreamble(out NanoFunction func) {
        // Begin generating the main results function.
        string[] functionModifiers = { "fragment" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, program.Float4Type, functionModifiers);

        // Identify which vertex shader is the input.
        this.vertexNode = graph.GetEdgeToDestinationOrNull(computeNode, "Verts")?.Source.Node as VertexShaderComputeNode;
        if (vertexNode == null) {
          NanoGraph.CurrentGenerateState.AddError($"Fragment shader {computeNode} is not connected to a vertex shader.");
          return;
        }
        this.vertexOp = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == vertexNode)?.Operation as VertexShaderComputeNode.EmitterGpu;
        if (vertexNode == null) {
          NanoGraph.CurrentGenerateState.AddError($"Vertex shader {vertexNode} for fragment shader {computeNode} is not ready.");
          return;
        }
        this.vertexType = vertexOp.resultTypeSpec.Type;
        if (vertexType == null) {
          NanoGraph.CurrentGenerateState.AddError($"Vertex shader {vertexNode} for fragment shader {computeNode} must output a struct.");
          return;
        }
        this.vertexProgramType = vertexOp.resultType;

        // Bind all other dependencies as buffers.
        int bufferIndex = 0;
        int inputIndex = 0;
        foreach (var computeInput in CollectComputeInputs(DependentComputeInputsToLoad)) {
          if (computeInput.Plug.Node == vertexNode) {
            inputIndex++;
            continue;
          }
          AddGpuFuncInput(func, computeInput, $"input{inputIndex++}", gpuInputBuffers, ref bufferIndex);
        }
        AddDebugGpuFuncInputs(func, gpuInputBuffers, ref bufferIndex);
        func.AddStatement($"#if defined(DEBUG)");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Bool)} isDebugThread = primitive_id == 0 && sample_id == 0;");
        func.AddStatement($"#endif // defined(DEBUG)");

        func.AddParam(Array.Empty<string>(), program.UintType, $"primitive_id", "[[primitive_id]]", new NanoParameterOptions { IsDebugOnly = true });
        func.AddParam(Array.Empty<string>(), program.UintType, $"sample_id", "[[sample_id]]", new NanoParameterOptions { IsDebugOnly = true });

        // Add a parameter that takes the vertex shader output.
        func.AddParam(Array.Empty<string>(), vertexProgramType, $"vertexData", "[[stage_in]]");
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        // TODO: Only need to return the color.
        result = default;
        var edge = graph.GetEdgeToDestinationOrNull(computeNode, "Color");
        if (edge == null) {
          NanoGraph.CurrentGenerateState.AddError($"The Color input for {computeNode} is not connected.");
          return;
        }
        CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
        if (inputLocal == null) {
          NanoGraph.CurrentGenerateState.AddError($"Input Color for {computeNode} is not defined.");
          return;
        }
        func.AddStatement($"return {inputLocal?.Identifier};");
        result = new CodeCachedResult { ResultType = resultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec } };
      }

      private string GetInputExpr(string fieldName) {
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
        return inputExpr;
      }

      private string EmitTextureSizeExpr(NanoFunction func) {
        switch (Node.SizeMode) {
          case TextureComputeSizeMode.Auto:
          case TextureComputeSizeMode.PatchSize:
          default:
            return "OutputTextureSize";
          case TextureComputeSizeMode.Custom:
            return func.EmitConvert(func.Program.Float2Type, func.Program.Int2Type, GetInputExpr("Size"));
        }
      }

      private static string ToMetalBlendOperation(BlendOperation value) {
        switch (value) {
          default:
          case BlendOperation.Add:
            return "MTLBlendOperationAdd";
          case BlendOperation.Subtract:
            return "MTLBlendOperationSubtract";
          case BlendOperation.ReverseSubtract:
            return "MTLBlendOperationSubtract";
          case BlendOperation.Min:
            return "MTLBlendOperationMax";
          case BlendOperation.Max:
            return "MTLBlendOperationMin";
        }
      }

      private static string ToMetalBlendFactor(BlendFactor value) {
        switch (value) {
          default:
          case BlendFactor.Zero:
            return "MTLBlendFactorZero";
          case BlendFactor.One:
            return "MTLBlendFactorOne";
          case BlendFactor.SourceColor:
            return "MTLBlendFactorSourceColor";
          case BlendFactor.OneMinusSourceColor:
            return "MTLBlendFactorOneMinusSourceColor";
          case BlendFactor.SourceAlpha:
            return "MTLBlendFactorSourceAlpha";
          case BlendFactor.OneMinusSourceAlpha:
            return "MTLBlendFactorOneMinusSourceAlpha";
          case BlendFactor.DestinationColor:
            return "MTLBlendFactorDestinationColor";
          case BlendFactor.OneMinusDestinationColor:
            return "MTLBlendFactorOneMinusDestinationColor";
          case BlendFactor.DestinationAlpha:
            return "MTLBlendFactorDestinationAlpha";
          case BlendFactor.OneMinusDestinationAlpha:
            return "MTLBlendFactorOneMinusDestinationAlpha";
        }
      }

      public override void EmitValidateCacheFunctionInner() {
        string pipelineStateIdentifier = program.AddInstanceField(program.MTLRenderPipelineState, $"{computeNode.ShortName}_RenderPipeline");
        string renderPassDescriptorIdentifier = program.AddInstanceField(program.MTLRenderPassDescriptor, $"{computeNode.ShortName}_RenderPassDescriptor");
        string renderTargetIdentifier = program.AddInstanceField(program.Texture, $"{computeNode.ShortName}_RenderTarget");

        // Identify which vertex shader is the input.
        VertexShaderComputeNode vertexNode = this.vertexNode;
        VertexShaderComputeNode.EmitterGpu vertexOp = this.vertexOp;
        if (vertexNode == null || vertexOp == null) {
          return;
        }
        FragmentShaderComputeNode fragmentNode = Node;
        FragmentShaderComputeNode.EmitterGpu fragmentOp = this;
        NanoFunction vertexFunc = vertexOp.func;
        NanoFunction fragmentFunc = fragmentOp.func;
        IReadOnlyList<NanoGpuBufferRef> gpuVertexInputBuffers = vertexOp.gpuInputBuffers;
        IReadOnlyList<NanoGpuBufferRef> gpuFragmentInputBuffers = fragmentOp.gpuInputBuffers;

        string EmitThreadCountExpr() {
          string VertexInputExpr() {
            string fieldName = vertexNode.VertexCountFields[0].Name;
            var edge = graph.GetEdgeToDestinationOrNull(vertexNode, fieldName);
            if (edge == null) {
              return null;
            }
            if (!(edge.Source.Node is IComputeNode sourceComputeNode)) {
              NanoGraph.CurrentGenerateState.AddError($"Node {vertexNode} depends on an output that is not a compute node ({edge.Source.Node}).");
              return null;
            }
            CodeCachedResult? sourceCachedResult = vertexOp.dependentComputeNodes.FirstOrNull(dependency => dependency.Node == sourceComputeNode)?.Result;
            if (sourceCachedResult == null) {
              NanoGraph.CurrentGenerateState.AddError($"Node {vertexNode} depends on a compute node that is not yet ready ({edge.Source.Node}).");
              return null;
            }

            return $"{sourceCachedResult?.Result.Identifier}.{sourceCachedResult?.ResultType.GetField(edge.Source.FieldName)}";
          }
          switch (vertexNode.VertexCountMode) {
            case VertexCountMode.Auto: {
              NanoGpuBufferRef primaryVertexBuffer = gpuVertexInputBuffers.FirstOrDefault(buffer => buffer.Type.IsArray);
              if (primaryVertexBuffer.Expression == null) {
                NanoGraph.CurrentGenerateState.AddError($"Cannot determine the number of vertices for {fragmentNode}. Its vertex shader should have an array input.");
                return null;
              }
              return $"GetLength({primaryVertexBuffer.Expression})";
            }
            case VertexCountMode.Integer:
              return VertexInputExpr();
            case VertexCountMode.ArraySize:
            default:
              return $"GetLength({VertexInputExpr()})";
          }
        }

        validateCacheFunction.AddStatement($"{vertexOp.validateCacheFunction.Identifier}();");

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

        // Count number of input vertices.
        string vertexCountLocal = validateCacheFunction.AllocLocal("VertexCount");
        string rawVertexCountExpr = EmitThreadCountExpr();
        if (rawVertexCountExpr == null) {
          return;
        }
        string vertexCountExpr = $"({rawVertexCountExpr}) * {validateCacheFunction.EmitLiteral(vertexNode.VertexMultiplier)}";

        // Set up render target if necessary.
        // TODO: Allow configuration of texture sizes.
        string bitDepthExpr = NanoGpuContext.BitDepthToMetal(graph.GetTextureBitDepth(Node.BitDepth));
        validateCacheFunction.AddStatement($"{renderTargetIdentifier} = ResizeTexture({renderTargetIdentifier}, {gridSizeXExpr}, {gridSizeYExpr}, {bitDepthExpr});");
        string outputTextureExpr = renderTargetIdentifier;

        // Sync buffers to GPU.
        EmitSyncBuffersToGpu(validateCacheFunction, gpuVertexInputBuffers.Concat(gpuFragmentInputBuffers).ToArray());

        // Create a MTLRenderCommandEncoder.
        validateCacheFunction.AddStatement($"{NanoProgram.IntIdentifier} {vertexCountLocal} = {vertexCountExpr};");
        validateCacheFunction.AddStatement($"MTLRenderPassDescriptor* renderPassDescriptor = {renderPassDescriptorIdentifier};");
        validateCacheFunction.AddStatement($"renderPassDescriptor.colorAttachments[0].texture = {outputTextureExpr};");
        validateCacheFunction.AddStatement($"id<MTLRenderCommandEncoder> encoder = [GetCurrentCommandBuffer() renderCommandEncoderWithDescriptor:renderPassDescriptor];");
        validateCacheFunction.AddStatement($"encoder.label = @\"{computeNode.ShortName}\";");
        validateCacheFunction.AddStatement($"[encoder setCullMode:MTLCullModeNone];");
        validateCacheFunction.AddStatement($"[encoder setRenderPipelineState:{pipelineStateIdentifier}];");

        // Bind all inputs for the vertex shader.
        EmitBindBuffers(validateCacheFunction, gpuVertexInputBuffers, variant: "Vertex");
        // Bind all inputs for the fragment shader.
        EmitBindBuffers(validateCacheFunction, gpuFragmentInputBuffers, variant: "Fragment");

        // Draw primitives.
        string primitiveTypeExpr;
        switch (vertexNode.GeometryType) {
          case GeometryType.Point:
            primitiveTypeExpr = "MTLPrimitiveTypePoint";
            break;
          case GeometryType.Line:
            primitiveTypeExpr = "MTLPrimitiveTypeLine";
            break;
          case GeometryType.LineStrip:
            primitiveTypeExpr = "MTLPrimitiveTypeLineStrip";
            break;
          case GeometryType.Triangle:
          default:
            primitiveTypeExpr = "MTLPrimitiveTypeTriangle";
            break;
          case GeometryType.TriangleStrip:
            primitiveTypeExpr = "MTLPrimitiveTypeTriangleStrip";
            break;
        }
        validateCacheFunction.AddStatement($"[encoder drawPrimitives:{primitiveTypeExpr} vertexStart:0 vertexCount:(uint)({vertexCountLocal})];");
        validateCacheFunction.AddStatement($"[encoder endEncoding];");

        // Store the result.
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{program.GetProgramType(cachedResult.Type).GetField("Out")} = {renderTargetIdentifier};");

        // Emit pipeline creation code.
        createPipelinesFunction.AddStatement($"{{");
        createPipelinesFunction.AddStatement($"  id<MTLFunction> vertexFunction = [defaultLibrary newFunctionWithName:@\"{vertexFunc.Identifier}\"];");
        createPipelinesFunction.AddStatement($"  id<MTLFunction> fragmentFunction = [defaultLibrary newFunctionWithName:@\"{fragmentFunc.Identifier}\"];");
        createPipelinesFunction.AddStatement($"  MTLRenderPipelineDescriptor *pipelineStateDescriptor = [[MTLRenderPipelineDescriptor alloc] init];");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.label = @\"{computeNode.ShortName}\";");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.vertexFunction = vertexFunction;");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.fragmentFunction = fragmentFunction;");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].pixelFormat = {bitDepthExpr};");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].blendingEnabled = {createPipelinesFunction.EmitLiteral(Node.BlendEnabled)};");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].alphaBlendOperation = {ToMetalBlendOperation(Node.AlphaBlendOperation)};");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].rgbBlendOperation = {ToMetalBlendOperation(Node.ColorBlendOperation)};");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].destinationAlphaBlendFactor = {ToMetalBlendFactor(Node.DestinationAlphaBlendFactor)};");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].destinationRGBBlendFactor = {ToMetalBlendFactor(Node.DestinationColorBlendFactor)};");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].sourceAlphaBlendFactor = {ToMetalBlendFactor(Node.SourceAlphaBlendFactor)};");
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].sourceRGBBlendFactor = {ToMetalBlendFactor(Node.SourceColorBlendFactor)};");

        createPipelinesFunction.AddStatement($"  {pipelineStateIdentifier} = [device newRenderPipelineStateWithDescriptor:pipelineStateDescriptor error:&error];");
        createPipelinesFunction.AddStatement($"  MTLRenderPassDescriptor* renderPassDescriptor = [MTLRenderPassDescriptor new];");
        createPipelinesFunction.AddStatement($"  renderPassDescriptor.colorAttachments[0].clearColor = MTLClearColorMake(0, 0, 0, 0);");
        createPipelinesFunction.AddStatement($"  renderPassDescriptor.colorAttachments[0].loadAction = MTLLoadActionClear;");
        createPipelinesFunction.AddStatement($"  renderPassDescriptor.colorAttachments[0].storeAction = MTLStoreActionStore;");

        createPipelinesFunction.AddStatement($"  {renderPassDescriptorIdentifier} = renderPassDescriptor;");
        createPipelinesFunction.AddStatement($"}}");
      }
    }
  }
}

