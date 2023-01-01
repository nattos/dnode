using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NanoGraph {
  [Serializable]
  public class TypeDeclBuilderField {
    public string Name = "";
    public PrimitiveType Primitive = PrimitiveType.Float;
    public bool IsArray = false;

    public TypeSpec AsTypeSpec() => TypeSpec.ToArray(TypeSpec.MakePrimitive(Primitive), IsArray);

    public static TypeDeclBuilderField Make(string name, PrimitiveType type) => new TypeDeclBuilderField { Name = name, Primitive = type };
  }

  [Serializable]
  public class TypeDeclBuilder {
    public List<TypeDeclBuilderField> Fields = new List<TypeDeclBuilderField>();

    public TypeField[] AsTypeFields() => (Fields ?? Enumerable.Empty<TypeDeclBuilderField>()).Select(field => new TypeField { Name = field.Name, Type = field.IsArray ? TypeSpec.MakeArray(TypeSpec.MakePrimitive(field.Primitive)) : TypeSpec.MakePrimitive(field.Primitive) }).ToArray();
  }

  public class TypeDeclNode : DataNode, ICompileTimeOnlyNode {
    [EditableAttribute]
    public bool IsArray;

    [EditableAttribute]
    public bool HasVertexPosition;

    [EditableAttribute]
    public TypeDeclBuilder Fields = new TypeDeclBuilder();
    public TypeDecl Type => new TypeDecl(AllTypeFields.ToArray());

    public override DataSpec InputSpec => DataSpec.Empty;
    public override DataSpec OutputSpec =>
        IsArray ? DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(TypeSpec.MakeType(Type))))
                : DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeType(Type)));

    public IEnumerable<TypeField> EditableTypeFields => Fields?.AsTypeFields() ?? Enumerable.Empty<TypeField>();
    public IEnumerable<TypeField> AllTypeFields {
      get {
        IEnumerable<TypeField> fields = EditableTypeFields;
        if (HasVertexPosition) {
          fields = new[] { new TypeField { Name = "Position", Type = TypeSpec.MakePrimitive(PrimitiveType.Float4), Attributes = new[] { "[[position]]" } } }.Concat(fields);
        }
        return fields;
      }
    }
  }
}
namespace Unity.VisualScripting.Generated.PropertyProviders {
  [SerializedPropertyProvider(typeof(NanoGraph.TypeDeclBuilder))]
  public class NanoGraph_TypeDeclBuilder : SerializedPropertyProvider<NanoGraph.TypeDeclBuilder> { }
}
