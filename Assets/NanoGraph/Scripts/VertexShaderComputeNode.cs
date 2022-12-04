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

    public override string EmitTotalThreadCount(NanoFunction func, CodeCachedResult cachedResult) {
      return func.EmitLiteral(1);
    }

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterGpu(this, context);
  
    public class EmitterGpu : EmitterBase {
      public readonly VertexShaderComputeNode Node;

      public EmitterGpu(VertexShaderComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public List<NanoGpuBufferRef> gpuInputBuffers = new List<NanoGpuBufferRef>();
      // public CodeCachedResult codeCachedResult;

      public override void EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc) {
        // Begin generating the main results function.
        string[] functionModifiers = { "vertex" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, paramTypes: Array.Empty<NanoProgramType>(), resultType, functionModifiers);
        this.arraySizesFunc = arraySizesFunc = program.AddFunction($"{computeNode.ShortName}_Sizes", NanoProgram.CpuContext, paramTypes: Array.Empty<NanoProgramType>(), arraySizeResultType);

        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint), $"gid_uint", "[[vertex_id]]");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");

        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        int inputIndex = 0;
        foreach (var computeInput in CollectComputeInputs(DependentComputeInputsToLoad)) {
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
        string returnLocal = func.AllocLocal("Return");
        string returnSizesLocal = arraySizesFunc.AllocLocal("Return");
        func.AddStatement($"{func.GetTypeIdentifier(resultType)} {returnLocal};");
        arraySizesFunc.AddStatement($"{arraySizesFunc.GetTypeIdentifier(arraySizeResultType)} {returnSizesLocal};");
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

          arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {inputLocal?.ArraySizeIdentifier};");
        }
        func.AddStatement($"return {returnLocal};");
        arraySizesFunc.AddStatement($"return {returnSizesLocal};");
        // Depending directly on the output of a vertex shader is invalid.
        // Only fragment shaders can depend on vertex shader outputs.
        result = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType };
      }

      public override void EmitValidateCacheFunction() {
        validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.VoidType);
        validateCacheFunction.AddStatement($"{validateSizesCacheFunction.Identifier}();");
        // All pipeline code goes in the fragment shader node.
      }


      protected override void EmitLoadOutput(CodeContext context, string fieldName, int inputIndex, CodeLocal intoLocal) {
        string outputLocal = intoLocal.Identifier;
        string outputSizeLocal = intoLocal.ArraySizeIdentifier;
        // TODO: Unwind this. Caller can provide expression.
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(intoLocal.Type)} {outputLocal} = vertexData.{program.GetProgramType(cachedResult.Type).GetField(fieldName)};");
        context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {outputSizeLocal} = {cachedResult.ArraySizeIdentifier}.{arraySizeResultType.GetField(fieldName)};");
      }
    }
  }
}

