using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NanoGraph {
  public enum BasicOutputType {
    Custom,
    Fields,
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
    protected override string ShortNamePart => $"{OutputType}ScalarCompute";

    [EditableAttribute]
    public TypeDeclBuilder Fields = new TypeDeclBuilder();

    protected override PrimitiveType? SingleFieldModeType => OutputType.ToPrimitiveTypeOrNull();
    protected override bool RequiresTypeDeclInput => OutputType == BasicOutputType.Fields ? false : base.RequiresTypeDeclInput;

    public override TypeField[] InputOutputTypeFields {
      get {
        if (OutputType == BasicOutputType.Fields) {
          return Fields?.AsTypeFields() ?? Array.Empty<TypeField>();
        }
        return base.InputOutputTypeFields;
      }
    }

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterCpu(this, context);

    protected class EmitterCpu : EmitterBase {
      public readonly ScalarComputeNode Node;

      public EmitterCpu(ScalarComputeNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionPreamble(out NanoFunction func) {
        // Begin generating the main results function.
        this.func = func = program.AddFunction(computeNode.ShortName, computeNode.CodeContext, resultType);
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        string returnLocal = func.AllocLocal("Return");
        func.AddStatement($"{func.GetTypeIdentifier(resultType)} {returnLocal};");
        TypeField[] outputFields = Node.OutputTypeFields;
        foreach (var field in outputFields) {
          string inputExpr;
          switch (Node.InputPortsMode) {
            case FieldPortsMode.Combined: {
              var edge = graph.GetEdgeToDestinationOrNull(computeNode, "Out");
              if (edge == null) {
                continue;
              }
              CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
              if (inputLocal == null) {
                NanoGraph.CurrentGenerateState.AddError($"Input {field.Name} for {computeNode} is not defined.");
                continue;
              }
              inputExpr = $"{inputLocal.Value.Identifier}.{program.GetProgramType(inputLocal.Value.Type).GetField(field.Name)}";
              break;
            }
            default:
            case FieldPortsMode.Individual: {
              var edge = graph.GetEdgeToDestinationOrNull(computeNode, field.Name);
              if (edge == null) {
                continue;
              }
              CodeLocal? inputLocal = resultLocalMap.GetOrNull(edge.Source);
              if (inputLocal == null) {
                NanoGraph.CurrentGenerateState.AddError($"Input {field.Name} for {computeNode} is not defined.");
                continue;
              }
              inputExpr = inputLocal.Value.Identifier;
              break;
            }
          }

          string outputExpr;
          switch (Node.OutputPortsMode) {
            case FieldPortsMode.Combined: {
              TypeSpec combinedOutType = computeOutputSpec.Fields.First(node => node.Name == "Out").Type;
              NanoProgramType programOutType = program.GetProgramType(combinedOutType);
              outputExpr = $"{returnLocal}.{resultType.GetField("Out")}.{programOutType.GetField(field.Name)}";
              break;
            }
            default:
            case FieldPortsMode.Individual:
              outputExpr = $"{returnLocal}.{resultType.GetField(field.Name)}";
              break;
          }

          func.AddStatement($"{outputExpr} = {inputExpr};");
        }
        func.AddStatement($"return {returnLocal};");
        result = new CodeCachedResult { ResultType = resultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec } };
      }

      public override void EmitValidateCacheFunctionInner() {
        // Treat buffers correctly.
        foreach (var dependency in dependentComputeNodes) {
          if (dependency.Result == null) {
            NanoGraph.CurrentGenerateState.AddError($"Dependency {dependency.Node} for {computeNode} not yet ready.");
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
        string funcResultLocal = func.AllocLocal("Result");
        validateCacheFunction.AddStatement($"{validateCacheFunction.GetTypeIdentifier(resultType)} {funcResultLocal} = {func.Identifier}();");
        foreach (var field in computeNode.OutputSpec.Fields) {
          if (field.IsCompileTimeOnly) {
            continue;
          }
          var fieldName = resultType.GetField(field.Name);
          if (field.Type.IsArray) {
            validateCacheFunction.AddStatement($"CopyCpuFrom({cachedResult.Identifier}.{fieldName}, ({funcResultLocal}.{fieldName}));");
          } else {
            validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName} = {funcResultLocal}.{fieldName};");
          }
        }
      }
    }
  }
}
