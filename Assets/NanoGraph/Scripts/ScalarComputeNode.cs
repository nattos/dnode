using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum BasicOutputType {
    Custom,
    Bool,
    Int,
    Uint,
    Uint2,
    Float,
    Float2,
    Float3,
    Float4,
    Texture,
  }

  public static class BasicOutputTypeExtensions {
    public static PrimitiveType? ToPrimitiveTypeOrNull(this BasicOutputType type) {
      switch (type) {
        case BasicOutputType.Custom:
        default:
          return null;
        case BasicOutputType.Bool: return PrimitiveType.Bool;
        case BasicOutputType.Int: return PrimitiveType.Int;
        case BasicOutputType.Uint: return PrimitiveType.Uint;
        case BasicOutputType.Uint2: return PrimitiveType.Uint2;
        case BasicOutputType.Float: return PrimitiveType.Float;
        case BasicOutputType.Float2: return PrimitiveType.Float2;
        case BasicOutputType.Float3: return PrimitiveType.Float3;
        case BasicOutputType.Float4: return PrimitiveType.Float4;
        case BasicOutputType.Texture: return PrimitiveType.Texture;
      }
    }
  }

  public class ScalarComputeNode : ComputeNode {
    public override INanoCodeContext CodeContext => NanoProgram.CpuContext;

    [EditableAttribute]
    public BasicOutputType OutputType = BasicOutputType.Custom;

    protected override PrimitiveType? SingleFieldModeType => OutputType.ToPrimitiveTypeOrNull();

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterCpu(this, context);

    protected class EmitterCpu : EmitterBase {
      public readonly ScalarComputeNode Node;

      public EmitterCpu(ScalarComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc) {
        // Begin generating the main results function.
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, resultType);
        this.arraySizesFunc = arraySizesFunc = program.AddFunction($"{computeNode.ShortName}_Sizes", NanoProgram.CpuContext, arraySizeResultType);
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
          func.AddStatement($"{returnLocal}.{resultType.GetField(field.Name)} = {inputLocal?.Identifier};");
          arraySizesFunc.AddStatement($"{returnSizesLocal}.{arraySizeResultType.GetField(field.Name)} = {inputLocal?.ArraySizeIdentifier};");
        }
        func.AddStatement($"return {returnLocal};");
        arraySizesFunc.AddStatement($"return {returnSizesLocal};");
        result = new CodeCachedResult { ResultType = resultType, ArraySizesResultType = arraySizeResultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec, ArraySizeIdentifier = cachedResult.ArraySizeIdentifier } };
      }

      public override void EmitValidateCacheFunction() {
        validateCacheFunction = program.AddFunction($"Update_{computeNode.ShortName}", NanoProgram.CpuContext, program.VoidType);
        validateCacheFunction.AddStatement($"{validateSizesCacheFunction.Identifier}();");
        // Treat buffers correctly.
        foreach (var dependency in dependentComputeNodes) {
          if (dependency.Result == null) {
            errors.Add($"Dependency {dependency.Node} for {computeNode} not yet ready.");
            continue;
          }
          string resultIdentifier = dependency.Result?.Result.Identifier;
          foreach (var field in dependency.Node.OutputSpec.Fields) {
            var fieldType = program.GetProgramType(field.Type, field.Name);
            if (fieldType.IsArray) {
              string inputBuffer = $"{resultIdentifier}.{dependency.Result?.ResultType.GetField(field.Name)}";
              func.AddStatement($"{inputBuffer}->SyncToCpu();");
            }
          }
        }
        string funcResultLocal = arraySizesFunc.AllocLocal("Result");
        validateCacheFunction.AddStatement($"{validateCacheFunction.GetTypeIdentifier(resultType)} {funcResultLocal} = {func.Identifier}();");
        foreach (var field in computeNode.OutputSpec.Fields) {
          if (field.IsCompileTimeOnly) {
            continue;
          }
          var fieldName = resultType.GetField(field.Name);
          if (field.Type.IsArray) {
            validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName}->CopyCpuFrom({funcResultLocal}.{fieldName}.get());");
          } else {
            validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName} = {funcResultLocal}.{fieldName};");
          }
        }
      }
    }
  }
}
