using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;

namespace NanoGraph {
  public class MathNode : BaseMathNode {
    [EditableAttribute]
    public new StandardOperatorType Operator = StandardOperatorType.Add;
    public override StandardOperatorType OperatorType => Operator;

    protected override string ShortNamePart => Operator.ToString();
  }

  public abstract class BaseMathNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    [EditableAttribute]
    public bool ForceIsArray = false;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(OutType, InternalElementType, forceIsArray: ForceIsArray ? true : null);
    public bool IsArrayInput => ElementType.IsArray;

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType, forceIsArray: ForceIsArray ? true : null);
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
      } else {
        string[] inputs = context.InputLocals.Select(input => input.Identifier).ToArray();
        string resultExpr = op.EmitExpressionCode(context, inputs);
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(outputElementType)} {outputIdentifier} = {resultExpr};");
      }
    }
  }

  public class MixNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float4);
    [EditableAttribute]
    public bool UseAlpha = true;
    protected override string ShortNamePart => $"{MixType}Mix";

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(OutType, InternalElementType);
    public bool IsArrayInput => ElementType.IsArray;

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType);
    }

    [EditableAttribute]
    public StandardMixType MixType = StandardMixType.Blend;
    public StandardMixer Operator => StandardMixers.Get(MixType);

    public int MultiInputCount = 2;

    public override IReadOnlyList<EditableAttribute> EditableAttributes {
      get {
        var baseAttribs = base.EditableAttributes;
        return baseAttribs.Concat(new[] {
          new EditableAttribute {
            Name = nameof(MultiInputCount),
            Type = MultiInputCount.GetType(),
            Getter = node => MultiInputCount,
            Setter = (node, value) => MultiInputCount = Math.Max(1, value as int? ?? 2),
          },
        }).ToArray();
      }
    }

    public override DataSpec InputSpec {
      get {
        var op = Operator;
        TypeSpec elementType = ElementType;
        int inputCount = MultiInputCount;
        DataField[] fields = new DataField[inputCount * 2];
        for (int i = 0; i < inputCount; ++i) {
          fields[(inputCount - i - 1) * 2 + 0] = DataField.MakeType(i.ToString(), elementType);
          fields[(inputCount - i - 1) * 2 + 1] = DataField.MakePrimitive($"{i} α", PrimitiveType.Float);
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
        elementType.IsArray = false;
        return elementType;
      }
    }

    public void EmitCode(CodeContext context) {
      var op = Operator;
      TypeSpec elementType = ElementType;
      TypeSpec outputElementType = OutputElementType;
      string outputIdentifier = context.OutputLocals[0].Identifier;
      StandardMixOptions options = new StandardMixOptions { UseAlpha = UseAlpha };
      int inputCount = MultiInputCount;
      if (IsArrayInput) {
        string outputLengthLocal = context.Function.AllocLocal("Length");
        string lengthExpr = StandardOperators.Get(StandardOperatorType.Max).EmitExpressionCode(context, context.InputLocals.Select(input => $"GetLength({input.Identifier})").ToArray());
        context.Function.AddStatement($"{NanoProgram.IntIdentifier} {outputLengthLocal} = {lengthExpr};");
        context.Function.AddStatement($"{context.Function.GetArrayTypeIdentifier(outputElementType)} {outputIdentifier}(NanoTypedBuffer<{context.Function.GetTypeIdentifier(outputElementType)}>::Allocate({outputLengthLocal}));");
        string indexLocal = context.Function.AllocLocal("Index");
        context.Function.AddStatement($"for ({NanoProgram.IntIdentifier} {indexLocal} = 0; {indexLocal} < {outputLengthLocal}; ++{indexLocal}) {{");

        (string, string)[] readInputExprs = new (string, string)[inputCount];
        for (int i = 0; i < inputCount; ++i) {
          string sampleInputExpr = context.Function.Context.EmitSampleBuffer(context.InputLocals[(inputCount - i - 1) * 2 + 0].Identifier, indexLocal);
          readInputExprs[i] = (sampleInputExpr, context.InputLocals[(inputCount - i - 1) * 2 + 1].Identifier);
        }
        string resultExpr = op.EmitExpressionCode(context, readInputExprs, options);
        string writeBufferExpr = context.Function.Context.EmitWriteBuffer(outputIdentifier, indexLocal, resultExpr);
        context.Function.AddStatement($"  {writeBufferExpr};");

        context.Function.AddStatement($"}}");
      } else {
        (string, string)[] inputs = new (string, string)[inputCount];
        for (int i = 0; i < inputCount; ++i) {
          inputs[i] = (context.InputLocals[(inputCount - i - 1) * 2 + 0].Identifier, context.InputLocals[(inputCount - i - 1) * 2 + 1].Identifier);
        }
        string resultExpr = op.EmitExpressionCode(context, inputs, options);
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(outputElementType)} {outputIdentifier} = {resultExpr};");
      }
    }
  }

  public class MakeArrayNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    [EditableAttribute]
    public int InputCount = 2;

    public override DataSpec InputSpec => DataSpec.FromFields(Enumerable.Range(0, InputCount).Select(i => DataField.MakeType(i.ToString(), ElementType)).ToArray());
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(ElementType)));

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(OutType, InternalElementType);

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType, forceIsArray: false);
    }

    public void EmitCode(CodeContext context) {
      context.Function.AddStatement($"{context.Function.GetArrayTypeIdentifier(ElementType)} {context.OutputLocals[0].Identifier}(NanoTypedBuffer<{context.Function.GetTypeIdentifier(ElementType)}>::Allocate({context.Function.EmitLiteral(InputCount)}));");
      for (int i = 0; i < InputCount; ++i) {
        context.Function.AddStatement($"{context.Function.Context.EmitWriteBuffer(context.OutputLocals[0].Identifier, context.Function.EmitLiteral(i), context.InputLocals[i].Identifier)};");
      }
    }
  }

  public interface IValueProvider {
    DataField[] GetExtraInputFields(TypeSpec elementType);
    IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges);
    PrimitiveType? PreferredType { get; }
    TypeSpec GetOutputElementType(TypeSpec elementType) => elementType;
    string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType);

    private static readonly IReadOnlyDictionary<ValueProviderType, IValueProvider> _valueProviders = new Dictionary<ValueProviderType, IValueProvider>() {
      { ValueProviderType.Value, new ValueValueProvider() },
      { ValueProviderType.Index, new IndexValueProvider() },
      { ValueProviderType.Random, new RandomValueProvider() },
      { ValueProviderType.Range, new RangeValueProvider() },
      { ValueProviderType.Step, new StepValueProvider() },
      { ValueProviderType.FrameNumber, new FrameNumberValueProvider() },
      { ValueProviderType.Time, new TimeValueProvider() },
      { ValueProviderType.DeltaTime, new DeltaTimeValueProvider() },
      { ValueProviderType.Length, new LengthValueProvider() },
      { ValueProviderType.TextureSize, new TextureSizeValueProvider() },
      { ValueProviderType.OutputTextureSize, new OutputTextureSizeValueProvider() },
      { ValueProviderType.Pi, new PiValueProvider() },
    };

    public static IValueProvider GetValueProvider(ValueProviderType type) {
      return _valueProviders.GetOrDefault(type);
    }
  }

  public enum ValueProviderType {
    Value,
    Index,
    Random,
    Range,
    Step,
    FrameNumber,
    Time,
    DeltaTime,
    Length,
    TextureSize,
    OutputTextureSize,
    Pi,
  }

  public class ValueValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => new[] { DataField.MakeType("Value", elementType) };
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => inputEdges.Where(edge => edge.Destination.FieldName == "Value");
    public PrimitiveType? PreferredType => null;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      return context.Function.EmitCopy(context.InputLocals[inputsIndexOffset].Identifier);
    }
  }

  public class IndexValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => Array.Empty<DataField>();
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Int;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      return context.Function.EmitConvert(TypeSpec.MakePrimitive(PrimitiveType.Int), elementType, indexExpr);
    }
  }

  public class RangeValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => new[] { DataField.MakeType("Min", elementType), DataField.MakeType("Max", elementType) };
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Float;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      string minIdentifier = context.InputLocals[inputsIndexOffset + 0].Identifier;
      string maxIdentifier = context.InputLocals[inputsIndexOffset + 1].Identifier;
      return $"lerp_op({minIdentifier}, {maxIdentifier}, ({indexExpr}) / ({context.Function.GetTypeIdentifier(PrimitiveType.Float)})(({lengthExpr}) - 1))";
    }
  }

  public class StepValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => new[] { DataField.MakeType("Start", elementType), DataField.MakeType("Step", elementType) };
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Float;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      string startIdentifier = context.InputLocals[inputsIndexOffset + 0].Identifier;
      string stepIdentifier = context.InputLocals[inputsIndexOffset + 1].Identifier;
      return $"{startIdentifier} + ({stepIdentifier} * ({indexExpr}))";
    }
  }

  public class RandomValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => Array.Empty<DataField>();
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Float;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      // Note: Only works in CPU context.
      if (!(context.Function.Context is NanoCpuContext)) {
        NanoGraph.CurrentGenerateState.AddError($"Random source only works in CPU contexts (for node {node}).");
      }
      NanoProgramType programType = context.Function.Program.GetProgramType(elementType);
      return $"random_next<{programType.Identifier}>()";
    }
  }

  public class FrameNumberValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => Array.Empty<DataField>();
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Int;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      // Note: Only works in CPU context.
      if (!(context.Function.Context is NanoCpuContext)) {
        NanoGraph.CurrentGenerateState.AddError($"Frame number source only works in CPU contexts (for node {node}).");
      }
      return context.Function.EmitConvert(TypeSpec.MakePrimitive(PrimitiveType.Int), elementType, "GetFrameNumber()");
    }
  }

  public class TimeValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => Array.Empty<DataField>();
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Double;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      // Note: Only works in CPU context.
      if (!(context.Function.Context is NanoCpuContext)) {
        NanoGraph.CurrentGenerateState.AddError($"Time source only works in CPU contexts (for node {node}).");
      }
      return context.Function.EmitConvert(TypeSpec.MakePrimitive(PrimitiveType.Double), elementType, "GetFrameTime()");
    }
  }

  public class DeltaTimeValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => Array.Empty<DataField>();
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Double;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      // Note: Only works in CPU context.
      if (!(context.Function.Context is NanoCpuContext)) {
        NanoGraph.CurrentGenerateState.AddError($"Time source only works in CPU contexts (for node {node}).");
      }
      return context.Function.EmitConvert(TypeSpec.MakePrimitive(PrimitiveType.Double), elementType, "GetFrameDeltaTime()");
    }
  }

  public class LengthValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => new[] { DataField.MakeType("In", TypeSpec.MakeArray(elementType)) };
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => inputEdges.Where(edge => edge.Destination.FieldName == "In");
    public PrimitiveType? PreferredType => null;
    public TypeSpec GetOutputElementType(TypeSpec elementType) => TypeSpec.MakePrimitive(PrimitiveType.Int);
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      return $"GetLength({context.InputLocals[inputsIndexOffset].Identifier})";
    }
  }

  public class TextureSizeValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => new[] { DataField.MakeType("In", TypeSpec.MakePrimitive(PrimitiveType.Texture)) };
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Float2;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      string textureSizeExpr = $"GetTextureSizeFloat({context.InputLocals[inputsIndexOffset].Identifier})";
      return context.Function.EmitConvert(TypeSpec.MakePrimitive(PrimitiveType.Float2), elementType, textureSizeExpr);
    }
  }

  public class OutputTextureSizeValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => Array.Empty<DataField>();
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Float2;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      string textureSizeExpr = $"OutputTextureSize";
      return context.Function.EmitConvert(context.Function.Program.Int2Type, context.Function.Program.GetProgramType(elementType), textureSizeExpr);
    }
  }

  public class PiValueProvider : IValueProvider {
    public DataField[] GetExtraInputFields(TypeSpec elementType) => Array.Empty<DataField>();
    public IEnumerable<DataEdge> FilteredAutoTypeEdges(IReadOnlyList<DataEdge> inputEdges) => Array.Empty<DataEdge>();
    public PrimitiveType? PreferredType => PrimitiveType.Float;
    public string EmitCode(ICodeNode node, CodeContext context, int inputsIndexOffset, string indexExpr, string lengthExpr, TypeSpec elementType) {
      return context.Function.EmitConvert(TypeSpec.MakePrimitive(PrimitiveType.Float), elementType, context.Function.EmitLiteral((float)Math.PI));
    }
  }

  public class FillArrayNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public ValueProviderType Source = ValueProviderType.Random;
    protected override string ShortNamePart => $"Fill{Source}";

    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    public IValueProvider ValueProvider => IValueProvider.GetValueProvider(Source);

    public override DataSpec InputSpec => DataSpec.FromFields(new[] { DataField.MakePrimitive("Length", PrimitiveType.Int) }.Concat(ValueProvider.GetExtraInputFields(ElementType)).ToArray());
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(OutputElementType)));

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(OutType, InternalElementType);
    public TypeSpec OutputElementType => ValueProvider.GetOutputElementType(AutoTypeUtils.GetAutoType(OutType, InternalElementType));

    public void UpdateTypesFromInputs() {
      if (ValueProvider.PreferredType is PrimitiveType preferredType) {
        InternalElementType.Primitive = preferredType;
      }
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType, forceIsArray: false);
    }

    public void EmitCode(CodeContext context) {
      IValueProvider provider = ValueProvider;
      var elementType = ElementType;
      var outputElementType = OutputElementType;
      string lengthExpr = context.InputLocals[0].Identifier;
      string lengthLocal = context.Function.AllocLocal("Length");
      context.Function.AddStatement($"{NanoProgram.IntIdentifier} {lengthLocal} = {lengthExpr};");
      context.Function.AddStatement($"{context.Function.GetArrayTypeIdentifier(outputElementType)} {context.OutputLocals[0].Identifier}(NanoTypedBuffer<{context.Function.GetTypeIdentifier(outputElementType)}>::Allocate({lengthLocal}));");
      string indexLocal = context.Function.AllocLocal("Index");
      context.Function.AddStatement($"for ({NanoProgram.IntIdentifier} {indexLocal} = 0; {indexLocal} < {lengthExpr}; ++{indexLocal}) {{");
      context.Function.AddStatement($"  {context.Function.Context.EmitWriteBuffer(context.OutputLocals[0].Identifier, indexLocal, provider.EmitCode(this, context, inputsIndexOffset: 1, indexLocal, lengthLocal, elementType))};");
      context.Function.AddStatement($"}}");
    }
  }

  public enum ReduceOperatorType {
    Length,
    Sum,
  }

  public class ReduceOperator {
    public readonly ReduceOperatorType Type;
    public readonly string FunctionIdentifier;
    public readonly PrimitiveType? ForcedResultType;

    public ReduceOperator(ReduceOperatorType type, string functionIdentifier, PrimitiveType? forcedResultType = null) {
      Type = type;
      FunctionIdentifier = functionIdentifier;
      ForcedResultType = forcedResultType;
    }
  }

  public static class ReduceOperators {
    public static readonly IReadOnlyList<ReduceOperator> All;
    public static readonly IReadOnlyDictionary<ReduceOperatorType, ReduceOperator> ByType;

    public static ReduceOperator Get(ReduceOperatorType type) => ByType[type];

    static ReduceOperators() {
      All = new[] {
          new ReduceOperator(ReduceOperatorType.Length, "GetLength", forcedResultType: PrimitiveType.Int),
          new ReduceOperator(ReduceOperatorType.Sum, "ArraySum"),
      };
      ByType = All.ToDictionary(op => op.Type, op => op);
    }
  }

  public class ReduceArrayNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public ReduceOperatorType Operation = ReduceOperatorType.Length;
    protected override string ShortNamePart => Operation.ToString();

    public ReduceOperator Operator => ReduceOperators.Get(Operation);

    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    public TypeSpec ElementType => TypeSpec.ToArray(AutoTypeUtils.GetAutoType(OutType, InternalElementType), false);
    public TypeSpec ReducedType => Operator.ForcedResultType != null ? TypeSpec.MakePrimitive(Operator.ForcedResultType.Value) : InternalElementType;

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetEdgeToDestinationOrNull(this, "In"), ref InternalElementType, forceIsArray: false);
    }

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakeType("In", TypeSpec.MakeArray(ElementType)));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", ReducedType));

    public void EmitCode(CodeContext context) {
      string funcName = Operator.FunctionIdentifier;
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(ReducedType)} {context.OutputLocals[0].Identifier} = {funcName}({context.InputLocals[0].Identifier});");
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
      context.Function.AddStatement($"{NanoProgram.IntIdentifier} {sizeLocal} = 0;");
      List<string> inputSizeLocals = new List<string>();
      for (int i = 0; i < InputCount; ++i) {
        string inputSizeLocal = context.Function.AllocLocal("InputSize");
        inputSizeLocals.Add(inputSizeLocal);
        context.Function.AddStatement($"{NanoProgram.IntIdentifier} {inputSizeLocal} = GetLength({context.InputLocals[i].Identifier});");
        context.Function.AddStatement($"{sizeLocal} += {inputSizeLocal};");
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
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    [EditableAttribute]
    public TextureFilterMode FilterMode = TextureFilterMode.Nearest;

    public TypeSpec ElementType => AutoTypeUtils.GetAutoType(OutType, InternalElementType);

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetEdgeToDestinationOrNull(this, "In"), ref InternalElementType, forceIsArray: false);
    }

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakeType("In", TypeSpec.MakeArray(InternalElementType)), DataField.MakeType("Index", TypeSpec.MakePrimitive((FilterMode == TextureFilterMode.Nearest) ? PrimitiveType.Int : PrimitiveType.Float)));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", ElementType));

    public void EmitCode(CodeContext context) {
      string inputExpr = context.InputLocals[0].Identifier;
      string indexExpr = context.InputLocals[1].Identifier;
      string sampleExpr;
      switch (FilterMode) {
        default:
        case TextureFilterMode.Nearest:
          sampleExpr = context.Function.Context.EmitSampleBuffer(inputExpr, indexExpr);
          break;
        case TextureFilterMode.Linear:
        case TextureFilterMode.Bicubic: {
          string indexLocal = context.Function.AllocLocal("Index");
          string index0Local = context.Function.AllocLocal("Index0");
          string index1Local = context.Function.AllocLocal("Index1");
          string alphaLocal = context.Function.AllocLocal("T");
          context.Function.AddStatement($"float {indexLocal} = {indexExpr};");
          context.Function.AddStatement($"int {index0Local} = (int)floor_op({indexLocal});");
          context.Function.AddStatement($"int {index1Local} = {index0Local} + 1;");
          context.Function.AddStatement($"float {alphaLocal} = {indexLocal} - {index0Local};");
          string sample0Expr = context.Function.Context.EmitSampleBuffer(inputExpr, index0Local);
          string sample1Expr = context.Function.Context.EmitSampleBuffer(inputExpr, index1Local);
          sampleExpr = $"lerp_op({sample0Expr}, {sample1Expr}, {alphaLocal})";
          break;
        }
      }
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(ElementType)} {context.OutputLocals[0].Identifier} = {sampleExpr};");
    }
  }

  public class ReadTextureNode : DataNode, ICodeNode {
    public enum CoordinateMode {
      Normal,
      Inverse,
    }

    [EditableAttribute]
    public TextureFilterMode FilterMode = TextureFilterMode.Linear;
    [EditableAttribute]
    public TextureWrapMode WrapMode = TextureWrapMode.Repeat;
    [EditableAttribute]
    public CoordinateMode Coordinates = CoordinateMode.Normal;
    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakePrimitive("In", PrimitiveType.Texture), DataField.MakePrimitive("Index", PrimitiveType.Float2));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakePrimitive("Out", PrimitiveType.Float4));

    public void EmitCode(CodeContext context) {
      string indexExpr = context.InputLocals[1].Identifier;
      switch (Coordinates) {
        case CoordinateMode.Inverse:
          indexExpr = $"(vector_float2 {{ ({indexExpr}).x, -({indexExpr}).y }})";
          break;
      }
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(context.OutputLocals[0].Type)} {context.OutputLocals[0].Identifier} = {context.Function.Context.EmitSampleTexture(context.InputLocals[0].Identifier, indexExpr, FilterMode, WrapMode)};");
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
    protected override string ShortNamePart => $"{Dimensions}Index";

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
    }
  }

  public enum PackableType {
    Custom,
    Float2,
    Float3,
    Float4,
  }

  public class PackableTypeInfo {
    public PackableType Type { get; private set; }
    public PrimitiveType Primitive { get; private set; }
    public IReadOnlyList<TypeField> Fields { get; private set; }

    private static readonly IReadOnlyDictionary<PackableType, PackableTypeInfo> _types = new Dictionary<PackableType, PackableTypeInfo> {
      {
        PackableType.Float2,
        new PackableTypeInfo {
          Type = PackableType.Float2,
          Primitive = PrimitiveType.Float2,
          Fields = new[] {
            TypeField.MakePrimitive("x", PrimitiveType.Float),
            TypeField.MakePrimitive("y", PrimitiveType.Float),
          }
        }
      },
      {
        PackableType.Float3,
        new PackableTypeInfo {
          Type = PackableType.Float3,
          Primitive = PrimitiveType.Float3,
          Fields = new[] {
            TypeField.MakePrimitive("x", PrimitiveType.Float),
            TypeField.MakePrimitive("y", PrimitiveType.Float),
            TypeField.MakePrimitive("z", PrimitiveType.Float),
          }
        }
      },
      {
        PackableType.Float4,
        new PackableTypeInfo {
          Type = PackableType.Float4,
          Primitive = PrimitiveType.Float4,
          Fields = new[] {
            TypeField.MakePrimitive("x", PrimitiveType.Float),
            TypeField.MakePrimitive("y", PrimitiveType.Float),
            TypeField.MakePrimitive("z", PrimitiveType.Float),
            TypeField.MakePrimitive("w", PrimitiveType.Float),
          }
        }
      },
    };
    private static readonly IReadOnlyDictionary<PrimitiveType, PackableTypeInfo> _primitiveTypes;

    static PackableTypeInfo() {
      _primitiveTypes = _types.Values.ToDictionary(type => type.Primitive, type => type);
    }

    public static PackableTypeInfo GetTypeInfo(PackableType type) {
      return _types.GetValueOrDefault(type);
    }

    public static PackableTypeInfo GetTypeInfo(PrimitiveType type) {
      return _primitiveTypes.GetValueOrDefault(type);
    }
  }

  public class PackNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public PackableType Type = PackableType.Custom;

    public bool IsArray = false;

    public TypeDeclMode TypeDeclMode = TypeDeclMode.External;
    public TypeDecl InternalType;

    public IReadOnlyList<DataEdge> Inputs;
    public IReadOnlyList<DataEdge> Outputs;

    void IAutoTypeNode.UpdateTypesFromInputs() {
      IsArray = false;
      foreach (var edge in Graph.GetInputEdges(this)) {
        if (edge.SourceFieldOrNull?.Type.IsArray == true) {
          IsArray = true;
          return;
        }
      }
    }

    public override DataSpec InputSpec {
      get {
        PackableTypeInfo packableTypeInfo = PackableTypeInfo.GetTypeInfo(Type);
        if (packableTypeInfo != null) {
          return DataSpec.FromFields(DataField.FromTypeFields(PackFromFields));
        }
        return DataSpec.FromFields(new[] { new DataField { Name = "TypeDecl", IsCompileTimeOnly = true, Type = TypeSpec.MakePrimitive(PrimitiveType.TypeDecl) } }.Concat(DataField.FromTypeFields(PackFromFields)).ToArray());
      }
    }
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", PackToType));

    public TypeSpec PackToType => TypeSpec.ToArray(PackToTypeElement, IsArray);
    public IReadOnlyList<TypeField> PackFromFields => TypeField.ToArray(PackFromFieldsElement, IsArray);

    public TypeSpec PackToTypeElement {
      get {
        PackableTypeInfo packableTypeInfo = PackableTypeInfo.GetTypeInfo(Type);
        if (packableTypeInfo != null) {
          return TypeSpec.MakePrimitive(packableTypeInfo.Primitive);
        }
        return TypeSpec.MakeType(CustomPackToTypeElement);
      }
    }

    public IReadOnlyList<TypeField> PackFromFieldsElement {
      get {
        PackableTypeInfo packableTypeInfo = PackableTypeInfo.GetTypeInfo(Type);
        if (packableTypeInfo != null) {
          return packableTypeInfo.Fields;
        }
        return CustomPackToTypeElement.Fields;
      }
    }

    public TypeDecl CustomPackToTypeElement {
      get {
        switch (TypeDeclMode) {
          case TypeDeclMode.Internal:
          default:
            return InternalType;
          case TypeDeclMode.External: {
            TypeSpec? field = Graph.GetEdgeToDestinationOrNull(new DataPlug { Node = this, FieldName = "TypeDecl" })?.SourceFieldOrNull?.Type;
            return field?.Type ?? TypeDecl.Empty;
          }
        }
      }
    }

    public void EmitCode(CodeContext context) {
      List<string> initializerParts = new List<string>();
      var resultType = context.Function.Program.GetProgramType(PackToType, this.ToString());
      var fields = PackFromFields;
      if (IsArray) {
        string outputLocalIdentifier = context.OutputLocals[0].Identifier;
        var elementType = context.Function.Program.GetProgramType(PackToTypeElement, this.ToString());
        string lengthExpr = StandardOperators.Get(StandardOperatorType.Max).EmitExpressionCode(context, context.InputLocals.Select(input => $"GetLength({input.Identifier})").ToArray());
        string inputLengthLocal = context.Function.AllocLocal("Length");
        context.Function.AddStatement($"{NanoProgram.IntIdentifier} {inputLengthLocal} = {lengthExpr};");

        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(resultType)} {outputLocalIdentifier}(NanoTypedBuffer<{context.Function.GetTypeIdentifier(elementType)}>::Allocate({inputLengthLocal}));");
        string indexLocal = context.Function.AllocLocal("Index");
        context.Function.AddStatement($"for ({NanoProgram.IntIdentifier} {indexLocal} = 0; {indexLocal} < {inputLengthLocal}; ++{indexLocal}) {{");
        for (int i = 0; i < fields.Count; ++i) {
          var field = fields[i];
          string fieldAssignment = elementType.IsBuiltIn ? "" : $".{elementType.GetField(field.Name)} = ";
          string inputExpr = context.Function.Context.EmitSampleBuffer(context.InputLocals[i].Identifier, indexLocal);
          initializerParts.Add($"{fieldAssignment}({context.Function.EmitConvert(context.InputLocals[i].Type, field.Type, inputExpr)}),");
        }
        string initializerStr = string.Join("\n", initializerParts);
        string constructElementExpr = $"{context.Function.GetTypeIdentifier(elementType)} {{ {initializerStr} }}";
        context.Function.AddStatement($"  {context.Function.Context.EmitWriteBuffer(outputLocalIdentifier, indexLocal, constructElementExpr)};");;
        context.Function.AddStatement($"}}");
      } else {
        for (int i = 0; i < fields.Count; ++i) {
          var field = fields[i];
          string fieldAssignment = resultType.IsBuiltIn ? "" : $".{resultType.GetField(field.Name)} = ";
          string inputIdentifier = context.InputLocals[i].Identifier;
          initializerParts.Add($"{fieldAssignment}({context.Function.EmitConvert(context.InputLocals[i].Type, field.Type, inputIdentifier)}),");
        }
        string initializerStr = string.Join("\n", initializerParts);
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(resultType)} {context.OutputLocals[0].Identifier} = {{ {initializerStr} }};");
      }
    }
  }

  public class UnpackNode : DataNode, ICodeNode, IAutoTypeNode {
    public TypeDeclMode TypeDeclMode = TypeDeclMode.External;
    public TypeSpec InternalType;

    public IReadOnlyList<DataEdge> Inputs;
    public IReadOnlyList<DataEdge> Outputs;

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakeType("In", PackToType));
    public override DataSpec OutputSpec => DataSpec.FromTypeFields(PackToTypeFields.ToArray());

    void IAutoTypeNode.UpdateTypesFromInputs() {}

    public TypeSpec PackToType {
      get {
        switch (TypeDeclMode) {
          case TypeDeclMode.Internal:
          default:
            return InternalType;
          case TypeDeclMode.External: {
            TypeSpec? field = Graph.GetEdgeToDestinationOrNull(new DataPlug { Node = this, FieldName = "In" })?.SourceFieldOrNull?.Type;
            if (field?.Primitive != null) {
              PrimitiveType primitiveType = field.Value.Primitive.Value;
              PackableTypeInfo packableTypeInfo = PackableTypeInfo.GetTypeInfo(primitiveType);
              if (packableTypeInfo != null) {
                bool isArray = field?.IsArray ?? false;
                return new TypeSpec { IsArray = isArray, Primitive = packableTypeInfo.Primitive };
              }
            }
            if (field == null) {
              bool isArray = field?.IsArray ?? false;
              return new TypeSpec { IsArray = isArray, Type = TypeDecl.Empty };
            }
            return field.Value;
          }
        }
      }
    }

    public IReadOnlyList<TypeField> PackToTypeFields {
      get {
        if (PackToType.IsArray) {
          return PackToTypeFieldsElementTypes.Select(field => new TypeField { Name = field.Name, Type = TypeSpec.MakeArray(field.Type) }).ToArray();
        }
        return PackToTypeFieldsElementTypes;
      }
    }

    public IReadOnlyList<TypeField> PackToTypeFieldsElementTypes {
      get {
        switch (TypeDeclMode) {
          case TypeDeclMode.Internal:
          default:
            return InternalType.Type.Fields;
          case TypeDeclMode.External: {
            TypeSpec? field = Graph.GetEdgeToDestinationOrNull(new DataPlug { Node = this, FieldName = "In" })?.SourceFieldOrNull?.Type;
            if (field?.Primitive != null) {
              PrimitiveType primitiveType = field.Value.Primitive.Value;
              PackableTypeInfo packableTypeInfo = PackableTypeInfo.GetTypeInfo(primitiveType);
              if (packableTypeInfo != null) {
                return packableTypeInfo.Fields;
              }
            }
            if (field == null || field.Value.Type == null) {
              return Array.Empty<TypeField>();
            }
            return field.Value.Type.Fields;
          }
        }
      }
    }

    public void EmitCode(CodeContext context) {
      CodeLocal inputLocal = context.InputLocals[0];
      NanoProgramType inputType = context.Function.Program.GetProgramType(inputLocal.Type);
      TypeSpec type = PackToType;
      IReadOnlyList<TypeField> typeFields = PackToTypeFieldsElementTypes;
      bool isArray = type.IsArray;
      if (isArray) {
        string inputLengthLocal = context.Function.AllocLocal("Length");
        context.Function.AddStatement($"{NanoProgram.IntIdentifier} {inputLengthLocal} = GetLength({inputLocal.Identifier});");
        int index = 0;
        foreach (var field in typeFields) {
          CodeLocal outputLocal = context.OutputLocals[index++];
          string outputArrayTypeIdentifier = context.Function.GetArrayTypeIdentifier(field.Type);
          string outputElementTypeIdentifier = context.Function.GetElementTypeIdentifier(field.Type);
          context.Function.AddStatement($"{outputArrayTypeIdentifier} {outputLocal.Identifier}(NanoTypedBuffer<{outputElementTypeIdentifier}>::Allocate({inputLengthLocal}));");
        }
        index = 0;
        string indexLocal = context.Function.AllocLocal("Index");
        context.Function.AddStatement($"for ({NanoProgram.IntIdentifier} {indexLocal} = 0; {indexLocal} < {inputLengthLocal}; ++{indexLocal}) {{");
        foreach (var field in typeFields) {
          CodeLocal outputLocal = context.OutputLocals[index++];
          string inputField = inputType.ElementType.IsBuiltIn ? field.Name : inputType.GetField(field.Name);
          string inputExpr = $"{context.Function.Context.EmitSampleBuffer(inputLocal.Identifier, indexLocal)}.{inputField}";
          context.Function.AddStatement($"  {context.Function.Context.EmitWriteBuffer(outputLocal.Identifier, indexLocal, inputExpr)};");
        }
        context.Function.AddStatement($"}}");
      } else {
        int index = 0;
        foreach (var field in typeFields) {
          CodeLocal outputLocal = context.OutputLocals[index++];
          string outputTypeIdentifier = context.Function.GetTypeIdentifier(field.Type);
          string inputField = inputType.IsBuiltIn ? field.Name : inputType.GetField(field.Name);
          context.Function.AddStatement($"{outputTypeIdentifier} {outputLocal.Identifier} = {inputLocal.Identifier}.{inputField};");
        }
      }
    }
  }

  public class SwitchNode : DataNode, ICodeNode, IConditionalNode, IAutoTypeNode {
    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalInputType = TypeSpec.MakePrimitive(PrimitiveType.Float);
    [EditableAttribute]
    public bool ForceIsArray = false;

    [EditableAttribute]
    public int InputCount = 2;

    public override DataSpec InputSpec => DataSpec.FromFields(new [] { DataField.MakePrimitive("Case", PrimitiveType.Int) }.Concat(Enumerable.Range(0, InputCount).Select(i => DataField.MakeType(i.ToString(), InputType))).ToArray());
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", InputType));

    public TypeSpec InputType => AutoTypeUtils.GetAutoType(OutType, InternalInputType, forceIsArray: ForceIsArray ? true : null);

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this).Where(edge => edge.Destination.FieldName != "Case").ToArray(), ref InternalInputType, forceIsArray: ForceIsArray ? true : null);
    }

    public void EmitCode(CodeContext context) {
      string outputIdentifier = context.OutputLocals[0].Identifier;
      string conditionExpr = context.InputLocals[0].Identifier;
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(InputType)} {outputIdentifier};");
      context.Function.AddStatement($"switch ({conditionExpr}) {{");
      for (int i = 1; i < context.InputLocals.Count; ++i) {
        int caseValue = i - 1;
        if (i == (context.InputLocals.Count - 1)) {
          context.Function.AddStatement($"  default:");
        } else {
          context.Function.AddStatement($"  case {context.Function.EmitLiteral(caseValue)}:");
        }
        context.Function.AddStatement($"    {outputIdentifier} = {context.InputLocals[i].Identifier};");
        context.Function.AddStatement($"    break;");
      }
      context.Function.AddStatement($"}}");
    }

    public void GetInputsAreConditional(bool[] outInputsAreConditional) {
      outInputsAreConditional[0] = false;
      for (int i = 1; i < outInputsAreConditional.Length; ++i) {
        outInputsAreConditional[i] = true;
      }
    }

    public void EmitInputsUsedCode(CodeContext context) {
      string conditionExpr = context.InputLocals[0].Identifier;
      context.Function.AddStatement($"switch ({conditionExpr}) {{");
      for (int i = 1; i < context.OutputLocals.Count; ++i) {
        int caseValue = i - 1;
        if (i == (context.InputLocals.Count - 1)) {
          context.Function.AddStatement($"  default:");
        } else {
          context.Function.AddStatement($"  case {context.Function.EmitLiteral(caseValue)}:");
        }
        context.Function.AddStatement($"    {context.OutputLocals[i].Identifier} = true;");
        context.Function.AddStatement($"    break;");
      }
      context.Function.AddStatement($"}}");
    }
  }

  public enum InputSource {
    Internal,
    External,
  }

  public class LiteralNode : DataNode, ICodeNode, IInternalNode {
    // TODO: Only allow values that can actually be input :P
    [EditableAttribute]
    public PrimitiveType Type = PrimitiveType.Float;
    protected override string ShortNamePart => Type.ToString();

    public InputSource ValueSource = InputSource.External;
    public DValue InternalValue;
    public Func<DValue> InternalValueProvider;

    bool IInternalNode.IsInternal => ValueSource == InputSource.Internal;

    public DValue EffectiveInternalValue => InternalValueProvider?.Invoke() ?? InternalValue;

    public TypeSpec OutTypeSpec => new TypeSpec { Primitive = Type, IsArray = IsArray };
    public bool IsArray => ValueSource == InputSource.Internal ? (EffectiveInternalValue.Rows > 1) : false;

    public override DataSpec InputSpec => ValueSource == InputSource.Internal ? DataSpec.Empty : DataSpec.FromFields(DataField.MakePrimitive("In", Type));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", OutTypeSpec));

    public void EmitCode(CodeContext context) {
      TypeSpec type = OutTypeSpec;
      if (ValueSource == InputSource.Internal) {
        var internalValue = EffectiveInternalValue;
        string internalValueExpr = context.Function.EmitLiteral(type, internalValue);
        string valueExpr = internalValueExpr;
        if (Graph.DebugEnabled && DebugEnabled) {
          context.DebugState.GetDebugSettableValuesFunction.AddStatement($"#if defined(DEBUG)");
          bool isGpuContext = context.Function.Context is NanoGpuContext;
          string debugValueKey = DebugValueKey;
          NanoProgramType debugStateType = isGpuContext ? context.DebugState.DebugGpuStateType : context.DebugState.DebugCpuStateType;
          string debugValueIdentifier = debugStateType.AddField(context.Function.Program.GetProgramType(type), debugValueKey, initializerStr: internalValueExpr);
          string accessPrefix = isGpuContext ? "debugState." : $"{context.DebugState.DebugCpuStateIdentifier}.";
          valueExpr = $"{accessPrefix}{debugValueIdentifier}";

          string decodeValuesExpr = null;
          switch (type.Primitive) {
            case PrimitiveType.Bool:
              decodeValuesExpr = $"(values.size() >= 1 ? (values[0] > 0.0 ? true : false) : false)";
              break;
            case PrimitiveType.Int:
              decodeValuesExpr = $"(values.size() >= 1 ? ((int)std::round(values[0])) : 0)";
              break;
            case PrimitiveType.Uint:
              decodeValuesExpr = $"(values.size() >= 1 ? ((uint)std::round(values[0])) : 0)";
              break;
            case PrimitiveType.Double:
              decodeValuesExpr = $"(values.size() >= 1 ? ((double)(values[0])) : 0.0)";
              break;
            case PrimitiveType.Float:
              decodeValuesExpr = $"(values.size() >= 1 ? ((float)(values[0])) : 0.0f)";
              break;
            case PrimitiveType.Float2:
              decodeValuesExpr = $"vector_float2 {{ (values.size() >= 1 ? ((float)(values[0])) : 0.0f), (values.size() >= 2 ? ((float)(values[1])) : 0.0f) }}";
              break;
            case PrimitiveType.Float3:
              decodeValuesExpr = $"vector_float3 {{ (values.size() >= 1 ? ((float)(values[0])) : 0.0f), (values.size() >= 2 ? ((float)(values[1])) : 0.0f), (values.size() >= 3 ? ((float)(values[2])) : 0.0f) }}";
              break;
            case PrimitiveType.Float4:
              decodeValuesExpr = $"vector_float4 {{ (values.size() >= 1 ? ((float)(values[0])) : 0.0f), (values.size() >= 2 ? ((float)(values[1])) : 0.0f), (values.size() >= 3 ? ((float)(values[2])) : 0.0f), (values.size() >= 4 ? ((float)(values[3])) : 0.0f) }}";
              break;
            default:
              break;
          }
          if (decodeValuesExpr != null) {
            string debugStatePtrExpr = isGpuContext ? "gpuState" : "cpuState";
            string debugKeyExpr = context.DebugState.GetDebugSettableValuesFunction.EmitLiteral(debugValueKey);
            context.DebugState.GetDebugSettableValuesFunction.AddStatement($"debugValues.push_back(DebugSettableValue {{ .Key = {debugKeyExpr}, .Setter = [{debugStatePtrExpr}](const std::vector<double>& values) {{ {debugStatePtrExpr}->{debugValueIdentifier} = {decodeValuesExpr}; }} }});");
          }
          context.DebugState.GetDebugSettableValuesFunction.AddStatement($"#endif // defined(DEBUG)");
        }
        context.Function.AddStatement($"#if defined(DEBUG)");
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(type)} {context.OutputLocals[0].Identifier} = {valueExpr};");
        context.Function.AddStatement($"#else // defined(DEBUG)");
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(type)} {context.OutputLocals[0].Identifier} = {internalValueExpr};");
        context.Function.AddStatement($"#endif // defined(DEBUG)");
      } else {
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(type)} {context.OutputLocals[0].Identifier} = {context.InputLocals[0].Identifier};");
      }
    }

    public string DebugValueKey => $"{DebugId}.value";
  }

  public class RouteNode : DataNode, ICodeNode, IAutoTypeNode, IInternalNode {
    public InputSource TypeSpecSource = InputSource.External;
    public TypeSpec InternalTypeSpec;

    public TypeSpec OutTypeSpec => TypeSpecSource == InputSource.Internal ? InternalTypeSpec : InternalElementType;

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakeType("In", OutTypeSpec));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", OutTypeSpec));

    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType);
    }

    bool IInternalNode.IsInternal => TypeSpecSource == InputSource.Internal;

    public void EmitCode(CodeContext context) {
      TypeSpec type = OutTypeSpec;
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(type)} {context.OutputLocals[0].Identifier} = {context.InputLocals[0].Identifier};");
    }
  }

  public class GenerateValueNode : DataNode, ICodeNode, IAutoTypeNode {
    [EditableAttribute]
    public ValueProviderType Source = ValueProviderType.Random;
    protected override string ShortNamePart => Source.ToString();

    [EditableAttribute]
    public AutoType OutType = AutoType.Auto;
    public TypeSpec InternalElementType = TypeSpec.MakePrimitive(PrimitiveType.Float);

    public IValueProvider ValueProvider => IValueProvider.GetValueProvider(Source);

    public override DataSpec InputSpec => DataSpec.FromFields(ValueProvider.GetExtraInputFields(ElementType));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", ElementType));

    public TypeSpec ElementType => PreferredElementType ?? AutoTypeUtils.GetAutoType(OutType, InternalElementType);
    public TypeSpec? PreferredElementType => PreferredType == null ? null : TypeSpec.MakePrimitive(PreferredType.Value);
    public PrimitiveType? PreferredType => OutType == AutoType.Auto ? ValueProvider.PreferredType : null;

    public void UpdateTypesFromInputs() {
      if (ValueProvider.PreferredType is PrimitiveType preferredType) {
        InternalElementType.Primitive = preferredType;
      }
      AutoTypeUtils.UpdateAutoType(Graph.GetInputEdges(this), ref InternalElementType, forceIsArray: false);
    }

    public void EmitCode(CodeContext context) {
      IValueProvider provider = ValueProvider;
      var elementType = ElementType;
      context.Function.AddStatement($"{context.Function.GetTypeIdentifier(elementType)} {context.OutputLocals[0].Identifier} = {provider.EmitCode(this, context, inputsIndexOffset: 0, context.Function.EmitLiteral(0), context.Function.EmitLiteral(1), elementType)};");
    }
  }
}
