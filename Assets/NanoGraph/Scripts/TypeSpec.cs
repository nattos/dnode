using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public class TypeField {
    public string Name;
    public TypeSpec Type;

    public static TypeField MakePrimitive(string name, PrimitiveType type) {
      return new TypeField { Name = name, Type = TypeSpec.MakePrimitive(type) };
    }

    public static TypeField MakeType(string name, TypeSpec type) {
      return new TypeField { Name = name, Type = type };
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
    Float,
    Float2,
    Float3,
    Float4,
    TypeDecl,
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
