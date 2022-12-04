using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public enum AutoType {
    Auto,
    Bool,
    Int,
    Float,
    Float2,
    Float3,
    Float4,
  }

  public interface IAutoTypeNode {
    void UpdateTypesFromInputs();
  }

  public static class AutoTypeUtils {
    public static TypeSpec GetAutoType(AutoType type, TypeSpec internalSpec) {
      switch (type) {
        case AutoType.Bool:
          return TypeSpec.MakePrimitive(PrimitiveType.Bool);
        case AutoType.Int:
          return TypeSpec.MakePrimitive(PrimitiveType.Int);
        case AutoType.Float:
          return TypeSpec.MakePrimitive(PrimitiveType.Float);
        case AutoType.Float2:
          return TypeSpec.MakePrimitive(PrimitiveType.Float2);
        case AutoType.Float3:
          return TypeSpec.MakePrimitive(PrimitiveType.Float3);
        case AutoType.Float4:
          return TypeSpec.MakePrimitive(PrimitiveType.Float4);
        case AutoType.Auto:
        default:
          return internalSpec;
      }
    }

    public static void UpdateAutoType(DataEdge edge, ref TypeSpec internalType, bool? forceIsArray = null) {
      if (edge == null) {
        return;
      }
      UpdateAutoType(new[] { edge }, ref internalType, forceIsArray);
    }

    public static void UpdateAutoType(IReadOnlyList<DataEdge> inputs, ref TypeSpec internalType, bool? forceIsArray = null) {
      foreach (DataEdge edge in inputs) {
        TypeSpec? sourceType = edge.SourceFieldOrNull?.Type;
        if (sourceType == null) {
          continue;
        }
        internalType.Primitive = sourceType.Value.Primitive;
        internalType.Type = sourceType.Value.Type;
        if (forceIsArray == null) {
          internalType.IsArray = sourceType.Value.IsArray;
        } else {
          internalType.IsArray = forceIsArray.Value;
        }
      }
    }
  }
}