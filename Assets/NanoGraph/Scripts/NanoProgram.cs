using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DNode;

namespace NanoGraph {
  public enum TextureFilterMode {
    Nearest,
    Linear,
    Bicubic,
  }

  public enum TextureWrapMode {
    ClampToZero,
    ClampToEdge,
    Repeat,
    MirroredRepeat,
  }

  public interface INanoCodeContext {
    string EmitBufferType(NanoProgramType type);
    string EmitWritableBufferType(NanoProgramType type);
    string EmitSampleBuffer(string source, string index);
    string EmitSampleTexture(string source, string index, TextureFilterMode filterMode, TextureWrapMode wrapMode);
    string EmitWriteBuffer(string destination, string index, string value);
    string EmitFunctionInput(NanoProgram program, CodeLocal cachedResult, string fieldName, int index);
    string EmitThreadId();
  }

  public class NanoCpuContext : INanoCodeContext {
    public string EmitBufferType(NanoProgramType type) => $"std::shared_ptr<NanoTypedBuffer<{type.EmitIdentifier(this)}>>";
    public string EmitWritableBufferType(NanoProgramType type) => $"std::shared_ptr<NanoTypedBuffer<{type.EmitIdentifier(this)}>>";
    public string EmitSampleBuffer(string source, string index) => $"SampleBuffer({source}, {index})";
    public string EmitSampleTexture(string source, string index, TextureFilterMode filterMode, TextureWrapMode wrapMode) => $"SampleTexture({source}, {index})"; // TODO: Invalid.
    public string EmitWriteBuffer(string destination, string index, string value) => $"WriteBuffer({destination}, {index}, {value})";
    public string EmitFunctionInput(NanoProgram program, CodeLocal cachedResult, string fieldName, int index) => $"{cachedResult.Identifier}.{program.GetProgramType(cachedResult.Type).GetField(fieldName)}";
    public string EmitThreadId() => throw new NotSupportedException();
  }

  public struct NanoGpuBufferRef {
    public string FieldName;
    public string Expression;
    public string ParameterName;
    public int Index;
    public NanoProgramType Type;
  }

  public class NanoGpuContext : INanoCodeContext {
    public string EmitBufferType(NanoProgramType type) => $"device const {type.EmitIdentifier(this)}*";
    public string EmitWritableBufferType(NanoProgramType type) => $"device {type.EmitIdentifier(this)}*";
    public string EmitSampleBuffer(string source, string index) => $"SampleBuffer({source}, {index})";
    public string EmitSampleTexture(string source, string index, TextureFilterMode filterMode, TextureWrapMode wrapMode) => $"SampleTexture<{TextureFilterModeToMetal(filterMode)}, {TextureWrapModeToMetal(wrapMode)}>({source}, {index})";
    public string EmitWriteBuffer(string destination, string index, string value) => $"WriteBuffer({destination}, {index}, {value})";
    public string EmitFunctionInput(NanoProgram program, CodeLocal cachedResult, string fieldName, int index) => $"input{index}";
    public string EmitThreadId() => "gid";

    public static string BitDepthToMetal(GlobalTextureBitDepth bitDepth) {
      switch (bitDepth) {
        default:
        case GlobalTextureBitDepth.Int8:
          return "MTLPixelFormatBGRA8Unorm_sRGB";
        case GlobalTextureBitDepth.Float16:
          return "MTLPixelFormatRGBA16Float";
        case GlobalTextureBitDepth.Float32:
          return "MTLPixelFormatRGBA32Float";
      }
    }

    private string TextureFilterModeToMetal(TextureFilterMode mode) {
      switch (mode) {
        default:
        case TextureFilterMode.Nearest:
          return "filter::nearest";
        case TextureFilterMode.Linear:
          return "filter::linear";
        case TextureFilterMode.Bicubic:
          return "filter::bicubic";
      }
    }

    private string TextureWrapModeToMetal(TextureWrapMode mode) {
      switch (mode) {
        case TextureWrapMode.ClampToZero:
          return "address::clamp_to_zero";
        case TextureWrapMode.ClampToEdge:
          return "address::clamp_to_edge";
        default:
        case TextureWrapMode.Repeat:
          return "address::repeat";
        case TextureWrapMode.MirroredRepeat:
          return "address::mirrored_repeat";
      }
    }
  }

  public struct NanoParameterOptions {
    public bool IsConst;
    public bool IsReference;
  }

  public class NanoFunction {
    public readonly NanoProgram Program;
    public readonly INanoCodeContext Context;
    public readonly string Identifier;
    public readonly NanoProgramType ReturnType;
    public readonly IReadOnlyList<string> Modifiers;

    private readonly List<(string[], NanoProgramType, string, string, NanoParameterOptions)> _params = new List<(string[], NanoProgramType, string, string, NanoParameterOptions)>();
    private readonly List<string> _locals = new List<string>();
    private readonly List<string> _statements = new List<string>();

    public NanoFunction(NanoProgram program, INanoCodeContext context, string identifier, NanoProgramType returnType, IReadOnlyList<string> modifiers) {
      Program = program;
      Context = context;
      Identifier = identifier;
      ReturnType = returnType;
      Modifiers = modifiers;
    }

    public void AddParam(string[] modifiers, NanoProgramType type, string identifier, string suffix, NanoParameterOptions options = default) {
      _params.Add((modifiers, type, identifier, suffix, options));
    }

    public string AllocLocal(string nameHint) {
      int index = _locals.Count;
      string name = $"local_{NanoProgram.SanitizeIdentifierFragment(nameHint)}_{index:D3}";
      _locals.Add(name);
      return name;
    }

    public void AddStatement(string line) {
      _statements.Add(line);
    }

    public string OuterCode {
      get {
        StringBuilder output = new StringBuilder();
        List<string> signatureParts = new List<string>();
        signatureParts.AddRange(Modifiers);
        signatureParts.Add(ReturnType.EmitIdentifier(Context));
        signatureParts.Add(Identifier);
        output.Append($"{string.Join(" ", signatureParts)}(");
        if (_params.Count > 1) {
          output.AppendLine();
          output.Append("    ");
        }
        output.Append(string.Join(",\n    ", _params.Select(param => {
          var options = param.Item5;
          List<string> parts = new List<string>();
          string modifierStr = string.Join(" ", param.Item1);
          if (!string.IsNullOrWhiteSpace(modifierStr)) {
            parts.Add(modifierStr);
          }
          string typeIdentifier;
          if (param.Item2.IsArray) {
            typeIdentifier = options.IsConst ? Context.EmitBufferType(param.Item2.ElementType) : Context.EmitWritableBufferType(param.Item2.ElementType);
          } else {
            typeIdentifier = GetTypeIdentifier(param.Item2) + (options.IsReference ? "&" : "");
          }
          parts.Add(typeIdentifier);
          parts.Add(param.Item3);
          if (!string.IsNullOrWhiteSpace(param.Item4)) {
            parts.Add(param.Item4);
          }
          return string.Join(" ", parts);
        })));
        output.AppendLine($") {{");
        foreach (string line in _statements) {
          output.AppendLine("  " + line);
        }
        output.AppendLine($"}}");
        return output.ToString();
      }
    }

    public string GetTypeIdentifier(NanoProgramType type) {
      return type?.EmitIdentifier(Context);
    }

    public string GetTypeIdentifier(TypeSpec type) {
      return Program.GetProgramType(type)?.EmitIdentifier(Context);
    }

    public string GetElementTypeIdentifier(TypeSpec type) {
      return Program.GetProgramType(type.ElementSpec)?.EmitIdentifier(Context);
    }

    public string GetArrayTypeIdentifier(TypeSpec type) {
      return Program.AddArrayType(Program.GetProgramType(type))?.EmitIdentifier(Context);
    }

    public string GetTypeIdentifier(PrimitiveType type) {
      return Program.GetPrimitiveType(type)?.EmitIdentifier(Context);
    }

    public string EmitLiteral(string value) {
      string escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
      return $"\"{escaped}\"";
    }

    public string EmitLiteral(bool value) {
      return value ? "true" : "false";
    }

    public string EmitLiteral(int value) {
      return value.ToString();
    }

    public string EmitUnsignedLiteral(int value) {
      return value.ToString();
    }

    public string EmitLiteral(float value) {
      if (float.IsNaN(value)) {
        return "std::numeric_limits<float>::quiet_NaN()";
      }
      if (float.IsPositiveInfinity(value)) {
        return "std::numeric_limits<float>::infinity()";
      }
      if (float.IsNegativeInfinity(value)) {
        return "(-std::numeric_limits<float>::infinity())";
      }
      string result = value.ToString("G9");
      if (!result.Contains(".")) {
        result = $"{result}.0";
      }
      return $"{result}f";
    }

    public string EmitLiteral(double value) {
      if (double.IsNaN(value)) {
        return "std::numeric_limits<double>::quiet_NaN()";
      }
      if (double.IsPositiveInfinity(value)) {
        return "std::numeric_limits<double>::infinity()";
      }
      if (double.IsNegativeInfinity(value)) {
        return "(-std::numeric_limits<double>::infinity())";
      }
      string result = value.ToString("G9");
      if (!result.Contains(".")) {
        result = $"{result}.0";
      }
      return result;
    }

    public string EmitUnsignedLiteral(UnityEngine.Vector2Int value) {
      return $"(vector_uint2 {{ {EmitLiteral(value.x)}, {EmitLiteral(value.y)} }})";
    }

    public string EmitLiteral(UnityEngine.Vector2 value) {
      return $"(vector_float2 {{ {EmitLiteral(value.x)}, {EmitLiteral(value.y)} }})";
    }

    public string EmitLiteral(UnityEngine.Vector3 value) {
      return $"(vector_float3 {{ {EmitLiteral(value.x)}, {EmitLiteral(value.y)}, {EmitLiteral(value.z)} }})";
    }

    public string EmitLiteral(UnityEngine.Vector4 value) {
      return $"(vector_float4 {{ {EmitLiteral(value.x)}, {EmitLiteral(value.y)}, {EmitLiteral(value.z)}, {EmitLiteral(value.w)} }})";
    }

    public string EmitLiteral(TypeSpec type, DValue value) {
      if (type.IsArray) {
        throw new NotSupportedException();
      }
      if (type.Primitive != null) {
        switch (type.Primitive.Value) {
          case PrimitiveType.Bool:
            return EmitLiteral(value.BoolFromRow(0));
          case PrimitiveType.Int:
            return EmitLiteral((int)Math.Round(value.FloatFromRow(0)));
          case PrimitiveType.Uint:
            return EmitUnsignedLiteral((int)Math.Round(value.FloatFromRow(0)));
          case PrimitiveType.Uint2:
            return EmitUnsignedLiteral(UnityEngine.Vector2Int.RoundToInt(value.Vector2FromRow(0)));
          case PrimitiveType.Double:
            return EmitLiteral(value[0, 0]);
          case PrimitiveType.Float:
            return EmitLiteral(value.FloatFromRow(0));
          case PrimitiveType.Float2:
            return EmitLiteral(value.Vector2FromRow(0));
          case PrimitiveType.Float3:
            return EmitLiteral(value.Vector3FromRow(0));
          case PrimitiveType.Float4:
            return EmitLiteral(value.Vector4FromRow(0));
        }
      }
      throw new NotSupportedException();
    }

    public string EmitCopy(string expr) {
      return $"copy_value({expr})";
    }

    // public bool CanConvert(TypeSpec fromType, TypeSpec toType) {
    // }

    public bool RequiresConvert(TypeSpec fromType, TypeSpec toType) {
      return RequiresConvert(Program.GetProgramType(fromType), Program.GetProgramType(toType));
    }

    public bool RequiresConvert(NanoProgramType fromType, NanoProgramType toType) {
      if (fromType == toType) {
        return false;
      }
      return true;
    }

    public string EmitConvert(TypeSpec fromType, TypeSpec toType, string expr) {
      return EmitConvert(Program.GetProgramType(fromType), Program.GetProgramType(toType), expr);
    }

    public string EmitConvert(NanoProgramType fromType, NanoProgramType toType, string expr) {
      if (fromType == toType) {
        return expr;
      }
      if (fromType.IsArray || toType.IsArray) {
        NanoProgramType fromElementType = fromType.IsArray ? fromType.ElementType : fromType;
        NanoProgramType toElementType = toType.IsArray ? toType.ElementType : toType;
        if (fromType.IsArray && !toType.IsArray) {
          string sampleExpr = Context.EmitSampleBuffer(expr, Context.EmitThreadId());
          return EmitConvert(fromElementType, toElementType, sampleExpr);
        }
        // Note: Doesn't work on GPU.
        if (Context is NanoGpuContext) {
          throw new NotSupportedException();
        }
        return $"ConvertArray<{fromElementType.Identifier}, {toElementType.Identifier}>({expr})";
      }
      if (fromType == Program.Texture) {
        return EmitConvert(Program.Float4Type, toType, Context.EmitSampleTexture(expr, "gid_xy_norm", TextureFilterMode.Linear, TextureWrapMode.Repeat));
      }
      if (fromType.IsBuiltIn && toType.IsBuiltIn) {
        return $"Convert<{GetTypeIdentifier(fromType)}, {GetTypeIdentifier(toType)}>({expr})";
      }
      throw new NotSupportedException();
    }
  }

  public class NanoProgramType {
    private struct Field {
      public NanoProgramType Type;
      public string Name;
      public IReadOnlyList<string> Attributes;
      public string InitializerStr;
    }

    public readonly NanoProgram Program;
    public readonly string Identifier;
    public readonly bool IsBuiltIn;
    public readonly bool IsArray;
    public readonly NanoProgramType ElementType;


    private readonly List<Field> _fields = new List<Field>();
    private readonly Dictionary<string, string> _fieldMap = new Dictionary<string, string>();

    public NanoProgramType(NanoProgram program, string identifier, bool isBuiltIn, bool isArray, NanoProgramType elementType) {
      Program = program;
      Identifier = identifier;
      IsBuiltIn = isBuiltIn;
      IsArray = isArray;
      ElementType = elementType;
    }

    public string EmitIdentifier(INanoCodeContext context) {
      if (IsArray) {
        return context.EmitBufferType(ElementType);
      }
      return Identifier;
    }

    public string AddField(NanoProgramType type, string nameHint, IReadOnlyList<string> attributes = null, string initializerStr = null) {
      attributes = attributes ?? Array.Empty<string>();
      int index = _fields.Count;
      string name = $"_{NanoProgram.SanitizeIdentifierFragment(nameHint)}_{index:D3}";
      _fields.Add(new Field { Type = type, Name = name, Attributes = attributes, InitializerStr = initializerStr });
      _fieldMap[nameHint] = name;
      return name;
    }

    public string GetField(string nameHint) {
      return _fieldMap.GetOrDefault(nameHint);
    }

    public string EmitOuterCode(INanoCodeContext context) {
      StringBuilder output = new StringBuilder();
      output.AppendLine($"struct {Identifier} {{");
      foreach (Field field in _fields) {
        NanoProgramType fieldType = field.Type;
        string name = field.Name;
        string suffix = "";
        if (field.Attributes?.Count > 0) {
          suffix = $" {string.Join(" ", field.Attributes)}";
        }
        string initializerPart = "";
        if (field.InitializerStr != null && context is NanoCpuContext) {
          initializerPart = $" = ({field.InitializerStr})";
        }
        string line = $"{fieldType.EmitIdentifier(context)} {name}{suffix}{initializerPart};";
        output.AppendLine("  " + line);
      }
      output.AppendLine($"}};");
      return output.ToString();
    }

    public static NanoProgramType MakeBuiltIn(NanoProgram program, string identifier) {
      return new NanoProgramType(program, identifier, isBuiltIn: true, isArray: false, elementType: null);
    }

    public static NanoProgramType MakeType(NanoProgram program, string identifier) {
      return new NanoProgramType(program, identifier, isBuiltIn: false, isArray: false, elementType: null);
    }

    public static NanoProgramType MakeArray(NanoProgram program, NanoProgramType elementType) {
      return new NanoProgramType(program, null, isBuiltIn: false, isArray: true, elementType: elementType);
    }
  }

  public struct NanoValueInputDecl {
    public string Name;
    public double DefaultValue;
    public double MinValue;
    public double MaxValue;
  }

  public class NanoProgram {
    public readonly string Identifier;

    public static readonly NanoCpuContext CpuContext = new NanoCpuContext();
    public static readonly NanoGpuContext GpuContext = new NanoGpuContext();

    public int TextureInputCount { get; private set; }

    private readonly List<NanoProgramType> _types = new List<NanoProgramType>();
    private readonly Dictionary<TypeDecl, NanoProgramType> _typeByDeclMap = new Dictionary<TypeDecl, NanoProgramType>();
    private readonly Dictionary<string, NanoProgramType> _typeByInnerCodeMap = new Dictionary<string, NanoProgramType>();
    private readonly Dictionary<NanoProgramType, NanoProgramType> _arrayTypesByElementType = new Dictionary<NanoProgramType, NanoProgramType>();
    private readonly List<NanoFunction> _functions = new List<NanoFunction>();
    private readonly List<string> _preambleStatements = new List<string>();

    private readonly List<string> _fields = new List<string>();
    private readonly List<string> _fieldsCode = new List<string>();
    private readonly Dictionary<string, string> _fieldMap = new Dictionary<string, string>();

    public IReadOnlyList<NanoValueInputDecl> ValueInputs => _valueInputs;
    private readonly List<NanoValueInputDecl> _valueInputs = new List<NanoValueInputDecl>();

    public static readonly string IntIdentifier = "int32_t";
    public static readonly string UintIdentifier = "uint32_t";
    public readonly NanoProgramType VoidType;
    public readonly NanoProgramType BoolType;
    public readonly NanoProgramType IntType;
    public readonly NanoProgramType Int2Type;
    public readonly NanoProgramType UintType;
    public readonly NanoProgramType Uint2Type;
    public readonly NanoProgramType DoubleType;
    public readonly NanoProgramType FloatType;
    public readonly NanoProgramType Float2Type;
    public readonly NanoProgramType Float3Type;
    public readonly NanoProgramType Float4Type;
    public readonly NanoProgramType Double4Type;
    public readonly NanoProgramType Texture;
    public readonly NanoProgramType WriteTexture;
    public readonly NanoProgramType MTLComputePipelineStateType;
    public readonly NanoProgramType MTLRenderPipelineState;
    public readonly NanoProgramType MTLRenderPassDescriptor;

    public NanoProgram(string name) {
      Identifier = $"Program_{SanitizeIdentifierFragment(name)}";
      _types.Add(VoidType = NanoProgramType.MakeBuiltIn(this, "void"));
      _types.Add(BoolType = NanoProgramType.MakeBuiltIn(this, "bool"));
      _types.Add(IntType = NanoProgramType.MakeBuiltIn(this, IntIdentifier));
      _types.Add(Int2Type = NanoProgramType.MakeBuiltIn(this, "vector_int2"));
      _types.Add(UintType = NanoProgramType.MakeBuiltIn(this, UintIdentifier));
      _types.Add(Uint2Type = NanoProgramType.MakeBuiltIn(this, "vector_uint2"));
      _types.Add(DoubleType = NanoProgramType.MakeBuiltIn(this, "double"));
      _types.Add(FloatType = NanoProgramType.MakeBuiltIn(this, "float"));
      _types.Add(Float2Type = NanoProgramType.MakeBuiltIn(this, "vector_float2"));
      _types.Add(Float3Type = NanoProgramType.MakeBuiltIn(this, "vector_float3"));
      _types.Add(Float4Type = NanoProgramType.MakeBuiltIn(this, "vector_float4"));
      _types.Add(Double4Type = NanoProgramType.MakeBuiltIn(this, "vector_double4"));
      _types.Add(Texture = NanoProgramType.MakeBuiltIn(this, "NanoTexture"));
      _types.Add(WriteTexture = NanoProgramType.MakeBuiltIn(this, "NanoWriteTexture"));
      _types.Add(MTLComputePipelineStateType = NanoProgramType.MakeBuiltIn(this, "id<MTLComputePipelineState>"));
      _types.Add(MTLRenderPipelineState = NanoProgramType.MakeBuiltIn(this, "id<MTLRenderPipelineState>"));
      _types.Add(MTLRenderPassDescriptor = NanoProgramType.MakeBuiltIn(this, "MTLRenderPassDescriptor*"));
    }

    public int AllocateTextureInput() {
      int index = TextureInputCount;
      ++TextureInputCount;
      return index;
    }

    public int AllocateValueInput(string name, double defaultValue, double minValue, double maxValue) {
      int index = _valueInputs.Count();
      _valueInputs.Add(new NanoValueInputDecl {
        Name = name,
        DefaultValue = defaultValue,
        MinValue = minValue,
        MaxValue = maxValue,
      });
      return index;
    }

    public void AddPreambleStatement(string line) {
      _preambleStatements.Add(line);
    }

    public NanoFunction AddOverrideFunction(string name, INanoCodeContext context, NanoProgramType returnType, string[] modifiers = null) {
      modifiers = modifiers ?? Array.Empty<string>();
      int index = _functions.Count;
      NanoFunction func = new NanoFunction(this, context, name, returnType, modifiers);
      _functions.Add(func);
      return func;
    }

    public NanoFunction AddFunction(string nameHint, INanoCodeContext context, NanoProgramType returnType, string[] modifiers = null) {
      modifiers = modifiers ?? Array.Empty<string>();
      int index = _functions.Count;
      string name = $"Func_{NanoProgram.SanitizeIdentifierFragment(nameHint)}_{index:D3}";
      NanoFunction func = new NanoFunction(this, context, name, returnType, modifiers);
      _functions.Add(func);
      return func;
    }

    public NanoProgramType AddArrayType(NanoProgramType elementType) {
      if (_arrayTypesByElementType.TryGetValue(elementType, out var cachedType)) {
        return cachedType;
      }
      NanoProgramType arrayType = NanoProgramType.MakeArray(this, elementType);
      _types.Add(arrayType);
      _arrayTypesByElementType[elementType] = arrayType;
      return arrayType;
    }

    public NanoProgramType AddType(TypeDecl type, string nameHint) {
      if (_typeByDeclMap.TryGetValue(type, out var cachedType)) {
        return cachedType;
      }
      string innerCode = BuildProtoType(type).EmitOuterCode(CpuContext);
      if (_typeByInnerCodeMap.TryGetValue(innerCode, out cachedType)) {
        return cachedType;
      }

      int index = _types.Count;
      string name = $"Type_{NanoProgram.SanitizeIdentifierFragment(nameHint)}_{index:D3}";
      NanoProgramType programType = NanoProgramType.MakeType(this, name);

      foreach (var field in type.Fields) {
        programType.AddField(GetProgramType(field.Type, nameHint: $"{nameHint}_{field.Name}"), field.Name, field.Attributes);
      }
      _types.Add(programType);
      _typeByDeclMap[type] = programType;
      _typeByInnerCodeMap[innerCode] = programType;
      return programType;
    }

    public NanoProgramType AddCustomType(string nameHint) {
      int index = _types.Count;
      string name = $"CustomType_{NanoProgram.SanitizeIdentifierFragment(nameHint)}_{index:D3}";
      NanoProgramType programType = NanoProgramType.MakeType(this, name);
      _types.Add(programType);
      return programType;
    }

    private NanoProgramType BuildProtoType(TypeDecl type) {
      NanoProgramType programType = NanoProgramType.MakeType(this, "Proto");
      foreach (var field in type.Fields) {
        programType.AddField(BuildProtoType(field.Type), field.Name, field.Attributes);
      }
      return programType;
    }
  
    private NanoProgramType BuildProtoType(TypeSpec type) {
      if (type.IsArray) {
        if (type.Primitive != null) {
          return BuildProtoArrayType(GetPrimitiveType(type.Primitive.Value));
        } else {
          return BuildProtoArrayType(BuildProtoType(type.Type));
        }
      }
      if (type.Primitive != null) {
        return GetPrimitiveType(type.Primitive.Value);
      }
      return BuildProtoType(type.Type);
    }

    private NanoProgramType BuildProtoArrayType(NanoProgramType type) {
      return NanoProgramType.MakeArray(this, type);
    }

    public NanoProgramType GetProgramType(TypeSpec type, string nameHint = null) {
      if (type.IsArray) {
        if (type.Primitive != null) {
          return AddArrayType(GetPrimitiveType(type.Primitive.Value));
        } else {
          return AddArrayType(AddType(type.Type, nameHint));
        }
      }
      if (type.Primitive != null) {
        return GetPrimitiveType(type.Primitive.Value);
      }
      return AddType(type.Type, nameHint);
    }

    public NanoProgramType GetPrimitiveType(PrimitiveType type) {
      switch (type) {
        case PrimitiveType.Bool:
          return BoolType;
        case PrimitiveType.Int:
          return IntType;
        case PrimitiveType.Uint:
          return UintType;
        case PrimitiveType.Uint2:
          return Uint2Type;
        case PrimitiveType.Double:
          return DoubleType;
        case PrimitiveType.Float:
          return FloatType;
        case PrimitiveType.Float2:
          return Float2Type;
        case PrimitiveType.Float3:
          return Float3Type;
        case PrimitiveType.Float4:
          return Float4Type;
        case PrimitiveType.Texture:
          return Texture;
      }
      return null;
    }

    public string AddInstanceField(NanoProgramType type, string nameHint, string initializerStr = null) {
      int index = _fields.Count;
      string name = $"_{NanoProgram.SanitizeIdentifierFragment(nameHint)}_{index:D3}";
      _fields.Add(name);
      string initializerPart = "";
      if (initializerStr != null) {
        initializerPart = $" = ({initializerStr})";
      }
      _fieldsCode.Add($"{type.EmitIdentifier(CpuContext)} {name}{initializerPart};");
      _fieldMap[nameHint] = name;
      return name;
    }

    public string GetInstanceField(string nameHint) {
      return _fieldMap.GetOrDefault(nameHint);
    }

    public string OuterCpuCode {
      get {
        StringBuilder output = new StringBuilder();
        foreach (string line in _preambleStatements) {
          output.AppendLine(line);
        }

        output.AppendLine($"class {Identifier} : public NanoProgram {{");
        output.AppendLine($"public:");

        output.AppendLine("// Types.");
        foreach (var type in _types) {
          if (type.IsBuiltIn || type.IsArray) {
            continue;
          }
          output.AppendLine(type.EmitOuterCode(CpuContext));
        }
        output.AppendLine("// Fields.");
        foreach (var fieldCode in _fieldsCode) {
          output.AppendLine(fieldCode);
        }
        output.AppendLine("");
        output.AppendLine("// Methods.");
        foreach (var func in _functions) {
          if (!(func.Context is NanoCpuContext)) {
            continue;
          }
          output.AppendLine(func.OuterCode);
        }

        output.AppendLine($"}};");

        return output.ToString();
      }
    }

    public string OuterGpuCode {
      get {
        StringBuilder output = new StringBuilder();
        output.AppendLine("// Types.");
        foreach (var type in _types) {
          if (type.IsBuiltIn || type.IsArray) {
            continue;
          }
          output.AppendLine(type.EmitOuterCode(GpuContext));
        }
        output.AppendLine("// Functions.");
        foreach (var func in _functions) {
          if (!(func.Context is NanoGpuContext)) {
            continue;
          }
          output.AppendLine(func.OuterCode);
        }

        return output.ToString();
      }
    }


    private static readonly System.Text.RegularExpressions.Regex _allowedIdentifierPattern = new System.Text.RegularExpressions.Regex(@"[A-z0-9_]");

    public static string SanitizeIdentifierFragment(string identifier) {
      string output = "";
      foreach (char c in identifier) {
        if (_allowedIdentifierPattern.IsMatch(c.ToString())) {
          output += c;
        } else {
          output += "_";
        }
      }
      return output;
    }
  }


}

