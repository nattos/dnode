using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public struct DataField {
    public string Name;
    public TypeSpec Type;
    public IReadOnlyList<string> Attributes;
    public bool IsCompileTimeOnly;

    public static DataField MakePrimitive(string name, PrimitiveType type) {
      return new DataField { Name = name, Type = TypeSpec.MakePrimitive(type) };
    }

    public static DataField MakeType(string name, TypeSpec type) {
      return new DataField { Name = name, Type = type };
    }

    public static DataField FromTypeField(TypeField field) {
      return new DataField { Name = field.Name, Type = field.Type, Attributes = field.Attributes };
    }

    public static DataField[] FromTypeFields(IEnumerable<TypeField> fields) {
      if (fields == null) {
        return Array.Empty<DataField>();
      }
      return fields.Select(FromTypeField).ToArray();
    }

    public override string ToString() {
      string result = $"{Type} {Name}";
      if (IsCompileTimeOnly) {
        result = $"constexpr {result}";
      }
      return result;
    }
  }

  public struct DataSpec {
    public IReadOnlyList<DataField> Fields;

    public static readonly DataSpec Empty = new DataSpec { Fields = Array.Empty<DataField>() };

    public static DataSpec FromFields(params DataField[] fields) {
      if (fields == null) {
        return Empty;
      }
      return new DataSpec { Fields = fields };
    }

    public static DataSpec FromTypeFields(params TypeField[] fields) {
      if (fields == null) {
        return Empty;
      }
      return new DataSpec { Fields = fields.Select(field => DataField.FromTypeField(field)).ToArray() };
    }

    public static DataSpec FromType(TypeDecl type) {
      if (type == null) {
        return Empty;
      }
      return FromTypeFields(type.Fields.ToArray());
    }

    public static DataSpec ExtendWithFields(DataSpec proto, params DataField[] extraFields) {
      return new DataSpec { Fields = proto.Fields.Concat(extraFields).ToArray() };
    }

    public static DataSpec ExtendWithFields(DataField[] extraFields, DataSpec proto) {
      return new DataSpec { Fields = extraFields.Concat(proto.Fields).ToArray() };
    }
  }

  public interface IDataNode {
    NanoGraph Graph { get; set; }
    DataSpec InputSpec { get; }
    DataSpec OutputSpec { get; }
    DataNodeCacheData CacheData { get; set; }
    string ShortName { get; }
    string DebugId { get; }

    void Validate(List<Action> cleanupActions);
  }
}
