using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class MathNode : BaseMathNode {
    [EditableAttribute]
    public new StandardOperatorType Operator = StandardOperatorType.Add;
    public override StandardOperatorType OperatorType => Operator;
  }

  public abstract class BaseMathNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public AutoType AutoType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(AutoType, InternalElementType);
    public bool IsArrayInput => ElementType.IsArray;

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType);
    }

    public abstract StandardOperatorType OperatorType { get; }
    public StandardOperator Operator => StandardOperators.Get(OperatorType);

    public int MultiInputCount = 2;

    public override IReadOnlyList<EditableAttribute> EditableAttributes {
      get {
        var baseAttribs = base.EditableAttributes;
        var op = Operator;
        if (op.IsMultipleInput) {
          return baseAttribs.Concat(new[] {
            new EditableAttribute {
              Name = nameof(MultiInputCount),
              Type = MultiInputCount.GetType(),
              Getter = node => MultiInputCount,
              Setter = (node, value) => MultiInputCount = Math.Max(1, value as int? ?? 2),
            },
          }).ToArray();
        }
        return baseAttribs;
      }
    }

    public override DataSpec InputSpec {
      get {
        var op = Operator;
        TypeSpec elementType = ElementType;
        int inputCount = op.InputCount ?? MultiInputCount;
        DataField[] fields = new DataField[inputCount];
        for (int i = 0; i < inputCount; ++i) {
          fields[i] = DataField.MakeType(i.ToString(), elementType);
        }
        return DataSpec.FromFields(fields);
      }
    }

    public override DataSpec OutputSpec {
      get {
        TypeSpec outputElementType = OutputElementType;
        bool isArray = IsArrayInput;
        TypeSpec outputType = isArray ? TypeSpec.MakeArray(outputElementType) : outputElementType;
        return DataSpec.FromFields(DataField.MakeType("Out", outputType));
      }
    }

    public TypeSpec OutputElementType {
      get {
        var op = Operator;
        TypeSpec elementType = ElementType;
        if (op.FixedOutputType != null) {
          return TypeSpec.MakePrimitive(op.FixedOutputType.Value);
        } else {
          elementType.IsArray = false;
          return elementType;
        }
      }
    }

    public void EmitCode(CodeContext context) {
      var op = Operator;
      TypeSpec elementType = ElementType;
      TypeSpec outputElementType = OutputElementType;
      string outputIdentifier = context.OutputLocals[0].Identifier;
      if (IsArrayInput) {
        string outputLengthLocal = context.Function.AllocLocal("Length");
        string lengthExpr = StandardOperators.Get(StandardOperatorType.Max).EmitExpressionCode(context, context.InputLocals.Select(input => $"GetLength({input.Identifier})").ToArray());
        context.Function.AddStatement($"{NanoProgram.IntIdentifier} {outputLengthLocal} = {lengthExpr};");
        context.Function.AddStatement($"{context.Function.GetArrayTypeIdentifier(outputElementType)} {outputIdentifier}(NanoTypedBuffer<{context.Function.GetTypeIdentifier(outputElementType)}>::Allocate({outputLengthLocal}));");
        string indexLocal = context.Function.AllocLocal("Index");
        context.Function.AddStatement($"for ({NanoProgram.IntIdentifier} {indexLocal} = 0; {indexLocal} < {outputLengthLocal}; ++{indexLocal}) {{");

        string[] readInputExprs = context.InputLocals.Select(input => context.Function.Context.EmitSampleBuffer(input.Identifier, indexLocal)).ToArray();
        string resultExpr = op.EmitExpressionCode(context, readInputExprs);
        string writeBufferExpr = context.Function.Context.EmitWriteBuffer(outputIdentifier, indexLocal, resultExpr);
        context.Function.AddStatement($"  {writeBufferExpr};");

        context.Function.AddStatement($"}}");

        string sizesLengthExpr = StandardOperators.Get(StandardOperatorType.Max).EmitExpressionCode(context, context.InputLocals.Select(input => $"{input.ArraySizeIdentifier}").ToArray());
        context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = {sizesLengthExpr};");
      } else {
        string[] inputs = context.InputLocals.Select(input => input.Identifier).ToArray();
        string resultExpr = op.EmitExpressionCode(context, inputs);
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(outputElementType)} {outputIdentifier} = {resultExpr};");
        context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = 1;");
      }
    }
  }

  public class MakeArrayNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public AutoType AutoType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    [EditableAttribute]
    public int InputCount;

    public override DataSpec InputSpec => DataSpec.FromFields(Enumerable.Range(0, InputCount).Select(i => DataField.MakeType(i.ToString(), ElementType)).ToArray());
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(ElementType)));

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(AutoType, InternalElementType);

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType, forceIsArray: false);
    }

    public void EmitCode(CodeContext context) {
      context.Function.AddStatement($"{context.Function.GetArrayTypeIdentifier(ElementType)} {context.OutputLocals[0].Identifier}(NanoTypedBuffer<{context.Function.GetTypeIdentifier(ElementType)}>::Allocate({context.Function.EmitLiteral(InputCount)}));");
      for (int i = 0; i < InputCount; ++i) {
        context.Function.AddStatement($"{context.Function.Context.EmitWriteBuffer(context.OutputLocals[0].Identifier, context.Function.EmitLiteral(i), context.InputLocals[i].Identifier)};");
      }
      context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = {InputCount};");
    }
  }

  public class ConcatNode : DataNode, ICodeNode {
    public TypeSpec ElementType;
    public int InputCount;

    public override DataSpec InputSpec => DataSpec.FromFields(Enumerable.Range(0, InputCount).Select(i => DataField.MakeType(i.ToString(), TypeSpec.MakeArray(ElementType))).ToArray());
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(ElementType)));

    public static ConcatNode Make(TypeSpec elementType, int inputCount) {
      return new ConcatNode { ElementType = elementType, InputCount = inputCount };
    }

    public void EmitCode(CodeContext context) {
      string sizeLocal = context.Function.AllocLocal("ConcatSize");
      string sizesSizeLocal = context.OutputLocals[0].ArraySizeIdentifier;
      context.Function.AddStatement($"{NanoProgram.IntIdentifier} {sizeLocal} = 0;");
      context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {sizesSizeLocal} = 0;");
      List<string> inputSizeLocals = new List<string>();
      for (int i = 0; i < InputCount; ++i) {
        string inputSizeLocal = context.Function.AllocLocal("InputSize");
        inputSizeLocals.Add(inputSizeLocal);
        context.Function.AddStatement($"{NanoProgram.IntIdentifier} {inputSizeLocal} = GetLength({context.InputLocals[i].Identifier});");
        context.Function.AddStatement($"{sizeLocal} += {inputSizeLocal};");
        context.ArraySizeFunction.AddStatement($"{sizesSizeLocal} += {context.InputLocals[i].ArraySizeIdentifier};");
      }
      context.Function.AddStatement($"{context.Function.GetArrayTypeIdentifier(ElementType)} {context.OutputLocals[0].Identifier}(NanoTypedBuffer<{context.Function.GetTypeIdentifier(ElementType)}>::Allocate({sizeLocal}));");
      string indexIdentifier = context.Function.AllocLocal("ConcatIndex");
      context.Function.AddStatement($"{NanoProgram.IntIdentifier} {indexIdentifier} = 0;");
      for (int i = 0; i < InputCount; ++i) {
        string inputSizeLocal = inputSizeLocals[i];
        string forIndexIdentifier = context.Function.AllocLocal("Index");
        context.Function.AddStatement($"for ({NanoProgram.IntIdentifier} {forIndexIdentifier} = 0; {forIndexIdentifier} < {inputSizeLocal}; ++{forIndexIdentifier}) {{");
        context.Function.AddStatement($"  {context.Function.Context.EmitWriteBuffer(context.OutputLocals[0].Identifier, indexIdentifier, context.Function.Context.EmitSampleBuffer(context.InputLocals[i].Identifier, forIndexIdentifier))};");
        context.Function.AddStatement($"  ++{indexIdentifier};");
        context.Function.AddStatement($"}}");
      }
    }
  }

  public class ReadNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public AutoType AutoType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(AutoType, InternalElementType);

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetEdgeToDestinationOrNull(this, "In"), ref InternalElementType, forceIsArray: false);
    }

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakeType("In", TypeSpec.MakeArray(ElementType)), DataField.MakeType("Index", TypeSpec.MakePrimitive(PrimitiveType.Int)));

    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", ElementType));

    public void EmitCode(CodeContext context) {
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(ElementType)} {context.OutputLocals[0].Identifier} = {context.Function.Context.EmitSampleBuffer(context.InputLocals[0].Identifier, context.InputLocals[1].Identifier)};");
      context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = 1;");
    }
  }

  public class ReadTextureNode : DataNode, ICodeNode {
    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakePrimitive("In", PrimitiveType.Texture), DataField.MakePrimitive("Index", PrimitiveType.Float2));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakePrimitive("Out", PrimitiveType.Float4));

    public void EmitCode(CodeContext context) {
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(context.OutputLocals[0].Type)} {context.OutputLocals[0].Identifier} = {context.Function.Context.EmitSampleTexture(context.InputLocals[0].Identifier, context.InputLocals[1].Identifier)};");
      context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = 1;");
    }
  }

  public class VectorIndexNode : DataNode, ICodeNode {
    public enum DimensionType {
      Linear,
      Grid2D,
      Grid3D,
      IntGrid2D,
      IntGrid3D,
    }

    [EditableAttribute]
    public DimensionType Dimensions = DimensionType.Linear;

    public override DataSpec InputSpec => DataSpec.Empty;

    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakePrimitive("Out", IndexType));

    public PrimitiveType IndexType {
      get {
        switch (Dimensions) {
          case DimensionType.Linear:
          default:
            return PrimitiveType.Int;
          case DimensionType.Grid2D:
            return PrimitiveType.Float2;
          case DimensionType.Grid3D:
            return PrimitiveType.Float3;
          // TODO: Support Int2 and Int3.
        }
      }
    }

    public void EmitCode(CodeContext context) {
      PrimitiveType indexType = IndexType;
      string indexExpr;
      switch (Dimensions) {
        case DimensionType.Linear:
        default:
          indexExpr = "gid";
          break;
        case DimensionType.Grid2D:
          indexExpr = "gid_xy_norm";
          break;
        case DimensionType.IntGrid2D:
          indexExpr = "gid_xy";
          break;
        case DimensionType.Grid3D:
          indexExpr = "gid_xyz_norm";
          break;
        case DimensionType.IntGrid3D:
          indexExpr = "gid_xyz";
          break;
      }

      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(indexType)} {context.OutputLocals[0].Identifier} = {indexExpr};");
      context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = 1;");
    }
  }

  // TODO: Pack primitives (ie. float4).
  public class PackNode : DataNode, ICodeNode {
    public TypeDeclMode TypeDeclMode = TypeDeclMode.External;
    public TypeDecl InternalType;

    public IReadOnlyList<DataEdge> Inputs;
    public IReadOnlyList<DataEdge> Outputs;

    public override DataSpec InputSpec => DataSpec.FromFields(new[] { new DataField { Name = "TypeDecl", IsCompileTimeOnly = true, Type = TypeSpec.MakePrimitive(PrimitiveType.TypeDecl) } }.Concat(DataField.FromTypeFields(PackToType?.Fields)).ToArray());
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeType(PackToType)));

    public TypeDecl PackToType {
      get {
        switch (TypeDeclMode) {
          case TypeDeclMode.Internal:
          default:
            return InternalType;
          case TypeDeclMode.External: {
            TypeSpec? field = Graph.GetEdgeToDestinationOrNull(new DataPlug { Node = this, FieldName = "TypeDecl" })?.SourceFieldOrNull?.Type;
            if (field?.IsArray == true) {
              return new TypeDecl(TypeField.MakeType("Out", field.Value));
            }
            return field?.Type;
          }
        }
      }
    }

    public void EmitCode(CodeContext context) {
      List<string> initializerParts = new List<string>();
      var resultType = context.Function.Program.GetProgramType(TypeSpec.MakeType(PackToType), this.ToString());
      for (int i = 0; i < PackToType.Fields.Count; ++i) {
        var field = PackToType.Fields[i];
        string inputIdentifier = context.InputLocals[i].Identifier;
        initializerParts.Add($".{resultType.GetField(field.Name)} = ({context.Function.EmitConvert(context.InputLocals[i].Type, field.Type, inputIdentifier)}),");
      }
      string initializerStr = string.Join("\n", initializerParts);
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(TypeSpec.MakeType(PackToType))} {context.OutputLocals[0].Identifier} = {{ {initializerStr} }};");
      context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = 1;");
    }
  }

  // TODO: Unpack primitives (ie. float4).
  public class UnpackNode : DataNode, ICodeNode, IAutoTypeNode {
    public TypeDeclMode TypeDeclMode = TypeDeclMode.External;
    public TypeSpec InternalType;

    public IReadOnlyList<DataEdge> Inputs;
    public IReadOnlyList<DataEdge> Outputs;

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakeType("In", PackToType));
    public override DataSpec OutputSpec {
      get {
        TypeSpec type = PackToType;
        bool isArray = type.IsArray;
        if (isArray) {
          return DataSpec.FromFields(PackToType.Type.Fields.Select(field => {
            return new DataField { Name = field.Name, Type = TypeSpec.MakeArray(field.Type) };
          }).ToArray());
        } else {
          return DataSpec.FromFields(PackToType.Type.Fields.Select(field => {
            return new DataField { Name = field.Name, Type = field.Type };
          }).ToArray());
        }
      }
    }

    void IAutoTypeNode.UpdateTypesFromInputs() {}

    public TypeSpec PackToType {
      get {
        switch (TypeDeclMode) {
          case TypeDeclMode.Internal:
          default:
            return InternalType;
          case TypeDeclMode.External: {
            TypeSpec? field = Graph.GetEdgeToDestinationOrNull(new DataPlug { Node = this, FieldName = "In" })?.SourceFieldOrNull?.Type;
            if (field == null || field?.Primitive != null) {
              bool isArray = field?.IsArray ?? false;
              return new TypeSpec { IsArray = isArray, Type = TypeDecl.Empty };
            }
            return field.Value;
          }
        }
      }
    }

    public void EmitCode(CodeContext context) {
      CodeLocal inputLocal = context.InputLocals[0];
      NanoProgramType inputType = context.Function.Program.GetProgramType(inputLocal.Type);
      TypeSpec type = PackToType;
      bool isArray = type.IsArray;
      if (isArray) {
        string inputLengthLocal = context.Function.AllocLocal("Length");
        context.Function.AddStatement($"{NanoProgram.IntIdentifier} {inputLengthLocal} = GetLength({inputLocal.Identifier});");
        int index = 0;
        foreach (var field in type.Type.Fields) {
          CodeLocal outputLocal = context.OutputLocals[index++];
          string outputArrayTypeIdentifier = context.Function.GetArrayTypeIdentifier(field.Type);
          string outputElementTypeIdentifier = context.Function.GetElementTypeIdentifier(field.Type);
          context.Function.AddStatement($"{outputArrayTypeIdentifier} {outputLocal.Identifier}(NanoTypedBuffer<{outputElementTypeIdentifier}>::Allocate({inputLengthLocal}));");
          context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {outputLocal.ArraySizeIdentifier} = {inputLocal.ArraySizeIdentifier};");
        }
        index = 0;
        string indexLocal = context.Function.AllocLocal("Index");
        context.Function.AddStatement($"for ({NanoProgram.IntIdentifier} {indexLocal} = 0; {indexLocal} < {inputLengthLocal}; ++{indexLocal}) {{");
        foreach (var field in type.Type.Fields) {
          CodeLocal outputLocal = context.OutputLocals[index++];
          string inputField = inputType.GetField(field.Name);
          string inputExpr = $"{context.Function.Context.EmitSampleBuffer(inputLocal.Identifier, indexLocal)}.{inputField}";
          context.Function.AddStatement($"  {context.Function.Context.EmitWriteBuffer(outputLocal.Identifier, indexLocal, inputExpr)};");
        }
        context.Function.AddStatement($"}}");
      } else {
        int index = 0;
        foreach (var field in type.Type.Fields) {
          CodeLocal outputLocal = context.OutputLocals[index++];
          string outputTypeIdentifier = context.Function.GetTypeIdentifier(field.Type);
          string inputField = inputType.GetField(field.Name);
          context.Function.AddStatement($"{outputTypeIdentifier} {outputLocal.Identifier} = {inputLocal.Identifier}.{inputField};");
          context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {outputLocal.ArraySizeIdentifier} = {inputLocal.ArraySizeIdentifier};");
        }
      }
    }
  }

  public enum InputSource {
    Internal,
    External,
  }

  public class LiteralNode : DataNode, ICodeNode {
    // TODO: Only allow values that can actually be input :P
    [EditableAttribute]
    public PrimitiveType Type = PrimitiveType.Float;

    public InputSource ValueSource = InputSource.External;
    public DValue InternalValue;
    public Func<DValue> InternalValueProvider;

    private DValue EffectiveInternalValue => InternalValueProvider?.Invoke() ?? InternalValue;

    public TypeSpec OutTypeSpec => new TypeSpec { Primitive = Type, IsArray = IsArray };
    public bool IsArray => ValueSource == InputSource.Internal ? (EffectiveInternalValue.Rows > 1) : false;

    public override DataSpec InputSpec => ValueSource == InputSource.Internal ? DataSpec.Empty : DataSpec.FromFields(DataField.MakePrimitive("In", Type));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", OutTypeSpec));

    public LiteralNode() {
    }

    public LiteralNode(PrimitiveType type) {
      Type = type;
    }

    public void EmitCode(CodeContext context) {
      TypeSpec type = OutTypeSpec;
      if (ValueSource == InputSource.Internal) {
        var internalValue = EffectiveInternalValue;
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(type)} {context.OutputLocals[0].Identifier} = {context.Function.EmitLiteral(type, internalValue)};");
        int length = IsArray ? internalValue.Rows : 1;
        context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = {context.ArraySizeFunction.EmitLiteral(length)};");
      } else {
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(type)} {context.OutputLocals[0].Identifier} = {context.InputLocals[0].Identifier};");
        context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {context.OutputLocals[0].ArraySizeIdentifier} = {context.ArraySizeFunction.EmitLiteral(1)};");
      }
    }
  }

  public class TypeDeclNode : DataNode, ICompileTimeOnlyNode {
    public class Field {
      public string Name = "";
      public PrimitiveType Primitive = PrimitiveType.Float;
      public bool IsArray = false;
    }

    [EditableAttribute]
    public bool IsArray;

    [EditableAttribute]
    public bool HasVertexPosition;

    public List<Field> EditableFields = new List<Field>();
    public TypeDecl Type => new TypeDecl(AllTypeFields.ToArray());

    public override DataSpec InputSpec => DataSpec.Empty;
    public override DataSpec OutputSpec =>
        IsArray ? DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(TypeSpec.MakeType(Type))))
                : DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeType(Type)));

    public IEnumerable<TypeField> EditableTypeFields => EditableFields.Select(field => new TypeField { Name = field.Name, Type = field.IsArray ? TypeSpec.MakeArray(TypeSpec.MakePrimitive(field.Primitive)) : TypeSpec.MakePrimitive(field.Primitive) });
    public IEnumerable<TypeField> AllTypeFields {
      get {
        IEnumerable<TypeField> fields = EditableTypeFields;
        if (HasVertexPosition) {
          fields = new[] { new TypeField { Name = "Position", Type = TypeSpec.MakePrimitive(PrimitiveType.Float4), Attributes = new[] { "[[position]]" } } }.Concat(fields);
        }
        return fields;
      }
    }

    public override IReadOnlyList<EditableAttribute> EditableAttributes {
      get {
        var baseAttributes = base.EditableAttributes;

        List<EditableAttribute> attribs = new List<EditableAttribute>();

        attribs.Add(new EditableAttribute {
          Name = $"Field Count",
          Type = typeof(int),
          Getter = node => EditableFields.Count,
          Setter = (node, value) => {
            int count = Math.Max(0, value as int? ?? 0);
            while (EditableFields.Count > count) {
              EditableFields.RemoveAt(EditableFields.Count - 1);
            }
            while (EditableFields.Count < count) {
              EditableFields.Add(new Field { Name = $"{EditableFields.Count}" });
            }
          },
        });

        int fieldIndex = 0;
        foreach (Field field in EditableFields) {
          attribs.Add(new EditableAttribute {
            Name = $"{fieldIndex} Name",
            Type = typeof(string),
            Getter = node => field.Name,
            Setter = (node, value) => field.Name = value as string ?? "",
          });
          attribs.Add(new EditableAttribute {
            Name = $"{fieldIndex} Type",
            Type = typeof(PrimitiveType),
            Getter = node => field.Primitive,
            Setter = (node, value) => field.Primitive = value as PrimitiveType? ?? PrimitiveType.Float,
          });
          attribs.Add(new EditableAttribute {
            Name = $"{fieldIndex} IsArray",
            Type = typeof(bool),
            Getter = node => field.IsArray,
            Setter = (node, value) => field.IsArray = value as bool? ?? false,
          });
          fieldIndex++;
        }

        return baseAttributes.Concat(attribs).ToArray();
      }
    }
  }
}
