using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  // A vertex shader requires exactly one input compute dependency, and it must be on an array input.
  // TODO: Allow vertex shaders to depend on other parameters (ie. as constants).
  public class VertexShaderComputeNode : ComputeNode {
    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;

    public override DataSpec OutputSpec {
      get {
        return DataSpec.ExtendWithFields(base.OutputSpec, new DataField { Name = "Verts", Type = TypeSpec.MakePrimitive(PrimitiveType.Vertices), IsCompileTimeOnly = true });
      }
    }

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterGpu(this, context);
  
    public class EmitterGpu : EmitterBase {
      public readonly VertexShaderComputeNode Node;

      public EmitterGpu(VertexShaderComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public List<NanoGpuBufferRef> gpuInputBuffers = new List<NanoGpuBufferRef>();
      // public CodeCachedResult codeCachedResult;

      public override void EmitFunctionPreamble(out NanoFunction func) {
        // Begin generating the main results function.
        string[] functionModifiers = { "vertex" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, resultType, functionModifiers);

        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint), $"gid_uint", "[[vertex_id]]");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");

        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        AddGpuFuncInputs(func, CollectComputeInputs(DependentComputeInputsToLoad), gpuInputBuffers, ref bufferIndex);
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        string returnLocal = func.AllocLocal("Return");
        func.AddStatement($"{func.GetTypeIdentifier(resultType)} {returnLocal};");
        foreach (var field in computeOutputSpec.Fields) {
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
          func.AddStatement($"{returnLocal}.{resultType.GetField(field.Name)} = {func.EmitConvert(inputLocal.Value.Type, field.Type, inputLocal?.Identifier)};");
          // For the output position, override the conversion logic to float4. For positions, the
          // w component should default to 1.0 otherwise it can get culled.
          if (field.Type.Primitive == PrimitiveType.Float4 &&
              field.Attributes.Contains("[[position]]") &&
              func.RequiresConvert(inputLocal.Value.Type, field.Type)) {
            func.AddStatement($"{returnLocal}.{resultType.GetField(field.Name)}.w = {func.EmitLiteral(1.0f)};");
          }
        }
        func.AddStatement($"return {returnLocal};");
        // Depending directly on the output of a vertex shader is invalid.
        // Only fragment shaders can depend on vertex shader outputs.
        result = new CodeCachedResult { ResultType = resultType };
      }

      public override void EmitValidateCacheFunction() {
        validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, program.VoidType);
        // All pipeline code goes in the fragment shader node.
      }


      protected override void EmitLoadOutput(CodeContext context, string fieldName, int inputIndex, CodeLocal intoLocal) {
        string outputLocal = intoLocal.Identifier;
        // TODO: Unwind this. Caller can provide expression.
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(intoLocal.Type)} {outputLocal} = vertexData.{program.GetProgramType(cachedResult.Type).GetField(fieldName)};");
      }
    }
  }
}

