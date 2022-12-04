using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  // A fragment shader takes the outputs of a vertex shader and turns that into a color.
  //
  // Its color input must depend on the outputs of exactly one vertex shader node, although it may
  // depend on any number of other scalar or vector compute nodes. It always outputs a texture.
  public class FragmentShaderComputeNode : ComputeNode {
    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakePrimitive("Color", PrimitiveType.Float4), new DataField { Name = "Verts", Type = TypeSpec.MakePrimitive(PrimitiveType.Vertices), IsCompileTimeOnly = true });

    public override DataSpec OutputSpec => DataSpec.FromFields(new DataField { Name = "Out", Type = TypeSpec.MakePrimitive(PrimitiveType.Texture) });

    public override string EmitTotalThreadCount(NanoFunction func, CodeCachedResult cachedResult) {
      return func.EmitLiteral(1);
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
      // public CodeCachedResult codeCachedResult;

      protected override IEnumerable<DataPlug> DependentComputeInputsToLoad => dependentComputeInputs.Where(plug => !(plug.Node == vertexNode && plug.FieldName == "Verts"));

      public override void EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc) {
        // Begin generating the main results function.
        string[] functionModifiers = { "fragment" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, paramTypes: Array.Empty<NanoProgramType>(), program.Float4Type, functionModifiers);
        this.arraySizesFunc = arraySizesFunc = program.AddFunction($"{computeNode.ShortName}_Sizes", NanoProgram.CpuContext, paramTypes: Array.Empty<NanoProgramType>(), arraySizeResultType);


        // Identify which vertex shader is the input.
        this.vertexNode = graph.GetEdgeToDestinationOrNull(computeNode, "Verts")?.Source.Node as VertexShaderComputeNode;
        if (vertexNode == null) {
          errors.Add($"Fragment shader {computeNode} is not connected to a vertex shader.");
          return;
        }
        this.vertexOp = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == vertexNode)?.Operation as VertexShaderComputeNode.EmitterGpu;
        if (vertexNode == null) {
          errors.Add($"Vertex shader {vertexNode} for fragment shader {computeNode} is not ready.");
          return;
        }
        this.vertexType = vertexOp.resultTypeSpec.Type;
        if (vertexType == null) {
          errors.Add($"Vertex shader {vertexNode} for fragment shader {computeNode} must output a struct.");
          return;
        }
        this.vertexProgramType = vertexOp.resultType;

        // Add a parameter that takes the vertex shader output.
        func.AddParam(Array.Empty<string>(), vertexProgramType, $"vertexData", "[[stage_in]]");

        // Bind all other dependencies as buffers.
        int bufferIndex = 0;
        int inputIndex = 0;
        foreach (var computeInput in CollectComputeInputs(DependentComputeInputsToLoad)) {
          if (computeInput.Plug.Node == vertexNode) {
            inputIndex++;
            continue;
          }
          gpuInputBuffers.Add(new NanoGpuBufferRef {
            FieldName = computeInput.Field.Name,
            Expression = computeInput.Expression,
            Index = bufferIndex,
            Type = computeInput.Field.Type,
          });
          var fieldType = computeInput.FieldType;
          string[] modifiers = { "constant", "const" };
          string suffix = $"[[buffer({bufferIndex++})]]";
          bool isReference = true;
          if (fieldType.IsArray) {
            modifiers = Array.Empty<string>();
            isReference = false;
          }
          func.AddParam(modifiers, fieldType, $"input{inputIndex++}", suffix, new NanoParameterOptions { IsConst = true, IsReference = isReference });
        }
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        // TODO: Only need to return the color.
        result = default;
        var edge = graph.GetEdgeToDestinationOrNull(computeNode, "Color");
        if (edge == null) {
          errors.Add($"The Color input for {computeNode} is not connected.");
          return;
        }
        CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
        if (inputLocal == null) {
          errors.Add($"Input Color for {computeNode} is not defined.");
          return;
        }
        func.AddStatement($"return {inputLocal?.Identifier};");

        string returnSizesLocal = arraySizesFunc.AllocLocal("Return");
        arraySizesFunc.AddStatement($"{arraySizesFunc.GetTypeIdentifier(arraySizeResultType)} {returnSizesLocal};");
        arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField("Out")} = {arraySizesFunc.EmitLiteral(1)};");
        arraySizesFunc.AddStatement($"return {returnSizesLocal};");

        result = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec, ArraySizeIdentifier = cachedResult.ArraySizeIdentifier } };
      }

      public override void EmitValidateCacheFunction() {
        string pipelineStateIdentifier = program.AddInstanceField(program.MTLRenderPipelineState, $"{computeNode.ShortName}_RenderPipeline");
        string renderPassDescriptorIdentifier = program.AddInstanceField(program.MTLRenderPassDescriptor, $"{computeNode.ShortName}_RenderPassDescriptor");
        string renderTargetIdentifier = program.AddInstanceField(program.Texture, $"{computeNode.ShortName}_RenderTarget");

        validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.VoidType);
        validateCacheFunction.AddStatement($"{validateSizesCacheFunction.Identifier}();");


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

        // Count number of input vertices.
        string vertexCountLocal = validateCacheFunction.AllocLocal("VertexCount");
        string vertexCountExpr;
        // TODO: Support other vertex count methods. For now just look at the first buffer input of the vertex shader.
        NanoGpuBufferRef primaryVertexBuffer = gpuVertexInputBuffers.FirstOrDefault(buffer => buffer.Type.IsArray);
        if (primaryVertexBuffer.Expression == null) {
          errors.Add($"Cannot determine the number of vertices for {fragmentNode}. Its vertex shader should have an array input.");
          return;
        }
        vertexCountExpr = $"GetLength({primaryVertexBuffer.Expression})";

        // Set up render target if necessary.
        // TODO: Allow configuration of texture sizes.
        validateCacheFunction.AddStatement($"{renderTargetIdentifier} = ResizeTexture({renderTargetIdentifier}, 1920, 1080);");
        string outputTextureExpr = renderTargetIdentifier;

        // Sync buffers to GPU.
        foreach (var inputBuffer in gpuVertexInputBuffers.Concat(gpuFragmentInputBuffers)) {
          if (inputBuffer.Type.IsArray) {
            validateCacheFunction.AddStatement($"{inputBuffer.Expression}->SyncToGpu();");
          }
        }

        // Create a MTLRenderCommandEncoder.
        validateCacheFunction.AddStatement($"{NanoProgram.IntIdentifier} {vertexCountLocal} = {vertexCountExpr};");
        validateCacheFunction.AddStatement($"MTLRenderPassDescriptor* renderPassDescriptor = {renderPassDescriptorIdentifier};");
        validateCacheFunction.AddStatement($"renderPassDescriptor.colorAttachments[0].texture = {outputTextureExpr};");
        validateCacheFunction.AddStatement($"id<MTLRenderCommandEncoder> renderEncoder = [GetCurrentCommandBuffer() renderCommandEncoderWithDescriptor:renderPassDescriptor];");
        validateCacheFunction.AddStatement($"renderEncoder.label = @\"{computeNode.ShortName}\";");
        validateCacheFunction.AddStatement($"[renderEncoder setCullMode:MTLCullModeNone];");
        validateCacheFunction.AddStatement($"[renderEncoder setRenderPipelineState:{pipelineStateIdentifier}];");

        // Bind all inputs for the vertex shader.
        foreach (var inputBuffer in gpuVertexInputBuffers) {
          string expression = inputBuffer.Expression;
          int bufferIndex = inputBuffer.Index;
          if (inputBuffer.Type.IsArray) {
            validateCacheFunction.AddStatement($"[renderEncoder setVertexBuffer:{expression}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
          } else {
            validateCacheFunction.AddStatement($"[renderEncoder setVertexBytes:&{expression} length:sizeof({expression}) atIndex:{bufferIndex}];");
          }
        }
        // Bind all inputs for the fragment shader.
        foreach (var inputBuffer in gpuFragmentInputBuffers) {
          string expression = inputBuffer.Expression;
          int bufferIndex = inputBuffer.Index;
          if (inputBuffer.Type.IsArray) {
            validateCacheFunction.AddStatement($"[renderEncoder setFragmentBuffer:{expression}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
          } else {
            validateCacheFunction.AddStatement($"[renderEncoder setFragmentBytes:&{expression} length:sizeof({expression}) atIndex:{bufferIndex}];");
          }
        }

        // Draw primitives.
        validateCacheFunction.AddStatement($"[renderEncoder drawPrimitives:MTLPrimitiveTypeTriangle vertexStart:0 vertexCount:(uint)({vertexCountLocal})];");
        validateCacheFunction.AddStatement($"[renderEncoder endEncoding];");

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
        createPipelinesFunction.AddStatement($"  pipelineStateDescriptor.colorAttachments[0].pixelFormat = MTLPixelFormatBGRA8Unorm;");
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

