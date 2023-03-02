using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum GeometryType {
    Point,
    Line,
    LineStrip,
    Triangle,
    TriangleStrip,
  }

  public enum VertexCountMode {
    Auto,
    Integer,
    ArraySize,
  }

  // A vertex shader requires exactly one input compute dependency, and it must be on an array input.
  // TODO: Allow vertex shaders to depend on other parameters (ie. as constants).
  public class VertexShaderComputeNode : ComputeNode, IAutoTypeNode {
    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;

    [EditableAttribute]
    public GeometryType GeometryType = GeometryType.Triangle;
    [EditableAttribute]
    public int VertexMultiplier = 1;

    [EditableAttribute]
    public VertexCountMode VertexCountMode = VertexCountMode.Auto;
    [EditableAttribute]
    public AutoType VertexCountFromArrayAutoType = AutoType.Auto;
    public TypeSpec VertexCountFromArrayElementType;

    public override DataSpec InputSpec => DataSpec.ExtendWithFields(VertexCountFields, base.InputSpec);

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetEdgeToDestinationOrNull(this, "VertexCountFromArray"), ref VertexCountFromArrayElementType, forceIsArray: false);
    }

    public DataField[] VertexCountFields {
      get {
        switch (VertexCountMode) {
          case VertexCountMode.Integer:
            return new[] { DataField.MakePrimitive("ThreadCount", PrimitiveType.Int) };
          default:
          case VertexCountMode.Auto:
            return Array.Empty<DataField>();
          case VertexCountMode.ArraySize:
            return new[] { DataField.MakeType("ThreadCountFromArray", TypeSpec.MakeArray(VertexCountFromArrayElementType)) };
        }
      }
    }

    public override DataSpec OutputSpec {
      get {
        return DataSpec.ExtendWithFields(new[] { new DataField { Name = "Verts", Type = TypeSpec.MakePrimitive(PrimitiveType.Vertices), IsCompileTimeOnly = true } }, base.OutputSpec);
      }
    }

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterGpu(this, context);
  
    public class EmitterGpu : EmitterBase {
      public readonly VertexShaderComputeNode Node;

      public EmitterGpu(VertexShaderComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public List<NanoGpuBufferRef> gpuInputBuffers = new List<NanoGpuBufferRef>();
      public List<NanoGpuExternalBufferRef> gpuExternalInputBuffers = new List<NanoGpuExternalBufferRef>();
      // public CodeCachedResult codeCachedResult;

      public override void EmitFunctionPreamble(out NanoFunction func) {
        // Begin generating the main results function.
        string[] functionModifiers = { "vertex" };
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, resultType, functionModifiers);

        // Load inputs.
        // Note: Only load inputs that we really read.
        int bufferIndex = 0;
        AddGpuFuncInputs(func, CollectComputeInputs(DependentComputeInputsToLoad), gpuInputBuffers, gpuExternalInputBuffers, ref bufferIndex);
        AddDebugGpuFuncInputs(func, gpuInputBuffers, ref bufferIndex);

        func.AddParam(Array.Empty<string>(), program.GetPrimitiveType(PrimitiveType.Uint), $"gid_uint", "[[vertex_id]]");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Int)} gid = gid_uint;");
        func.AddStatement($"#if defined(DEBUG)");
        func.AddStatement($"{func.GetTypeIdentifier(PrimitiveType.Bool)} isDebugThread = gid == 0;");
        func.AddStatement($"#endif // defined(DEBUG)");
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        string returnLocal = func.AllocLocal("Return");
        func.AddStatement($"{func.GetTypeIdentifier(resultType)} {returnLocal};");
        switch (Node.InputPortsMode) {
          case FieldPortsMode.Combined: {
            var edge = graph.GetEdgeToDestinationOrNull(computeNode, "Out");
            if (edge == null) {
              break;
            }
            CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
            if (inputLocal == null) {
              NanoGraph.CurrentGenerateState.AddError($"Input Out for {computeNode} is not defined.");
              break;
            }
            func.AddStatement($"{returnLocal} = {inputLocal.Value.Identifier};");
            break;
          }
          default:
          case FieldPortsMode.Individual: {
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
                NanoGraph.CurrentGenerateState.AddError($"Input {field.Name} for {computeNode} is not defined.");
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
            break;
          }
        }
        func.AddStatement($"return {returnLocal};");
        // Depending directly on the output of a vertex shader is invalid.
        // Only fragment shaders can depend on vertex shader outputs.
        result = new CodeCachedResult { ResultType = resultType };
      }

      public override void EmitValidateCacheFunctionInner() {
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

