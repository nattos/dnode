using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public class TypeField {
    public string Name;
    public TypeSpec Type;
    public IReadOnlyList<string> Attributes = Array.Empty<string>();

    public static TypeField MakePrimitive(string name, PrimitiveType type) {
      return new TypeField { Name = name, Type = TypeSpec.MakePrimitive(type) };
    }

    public static TypeField MakeType(string name, TypeSpec type) {
      return new TypeField { Name = name, Type = type };
    }

    public static TypeField ToArray(TypeField field, bool isArray) {
      if (isArray) {
        return new TypeField { Name = field.Name, Attributes = field.Attributes, Type = TypeSpec.MakeArray(field.Type) };
      }
      return field;
    }

    public static IReadOnlyList<TypeField> ToArray(IReadOnlyList<TypeField> fields, bool isArray) {
      if (isArray) {
        return fields.Select(field => ToArray(field, true)).ToArray();
      }
      return fields;
    }
  }

  public class TypeDecl {
    public readonly IReadOnlyList<TypeField> Fields;

    public TypeDecl(params TypeField[] fields) {
      Fields = fields;
    }

    public static readonly TypeDecl Empty = new TypeDecl();
  }

  public enum PrimitiveType {
    Bool,
    Int,
    Uint,
    Uint2,
    Double,
    Float,
    Float2,
    Float3,
    Float4,
    Texture,
    TypeDecl,
    Vertices,
    BufferRef,
  }

  public struct TypeSpec {
    public bool IsArray;
    public PrimitiveType? Primitive;
    public TypeDecl Type;

    public TypeSpec ElementSpec => new TypeSpec { Primitive = Primitive, Type = Type };

    public override string ToString() {
      string result;
      if (Primitive != null) {
        result = Primitive.ToString();
      } else if (Type != null) {
        result = "struct";
      } else {
        result = "undefined";
      }
      if (IsArray) {
        result = $"{result}[]";
      }
      return result;
    }

    public static TypeSpec MakePrimitive(PrimitiveType type) {
      return new TypeSpec { Primitive = type };
    }

    public static TypeSpec MakeType(TypeDecl type) {
      return new TypeSpec { Type = type };
    }

    public static TypeSpec MakeArray(TypeSpec elementType) {
      if (elementType.Primitive != null) {
        return new TypeSpec { IsArray = true, Primitive = elementType.Primitive };
      }
      return new TypeSpec { IsArray = true, Type = elementType.Type };
    }

    public static TypeSpec ToArray(TypeSpec elementType, bool isArray) {
      if (isArray) {
        return MakeArray(elementType);
      }
      return elementType;
    }

    public static bool IsConvertible(TypeSpec a, TypeSpec b) {
      if (a.IsArray != b.IsArray) {
        return false;
      }
      if (a.Primitive != null || b.Primitive != null) {
        return a.Primitive == b.Primitive;
      }
      // TODO: Support TypeDecls.
      return false;
    }
  }
}
