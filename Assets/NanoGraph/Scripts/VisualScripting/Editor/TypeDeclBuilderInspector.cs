using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace NanoGraph {
  public class TypeDeclBuilderInspector {
    private class EditableAttributesWrapper : IEditableAttributeProvider {
      public IReadOnlyList<EditableAttribute> EditableAttributes { get; set; }
      public EditableAttributesWrapper(IReadOnlyList<EditableAttribute> attributes) {
        EditableAttributes = attributes;
      }
    }

    public static float GetHeight(float width, string label, TypeDeclBuilder oldValue) {
      float height = 0.0f;
      if (label != null) {
        height += EditorGUIUtility.singleLineHeight * 3;
      }
      height += EditableAttributesInspector.GetHeight(width, new EditableAttributesWrapper(GetEditableAttributes(oldValue, out _)));
      return height;
    }

    public static TypeDeclBuilder OnGUI(Rect rect, string label, TypeDeclBuilder oldValue) {
      Rect fullRect = rect;
      if (label != null) {
        rect.y += EditorGUIUtility.singleLineHeight;
        rect.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(rect, label);
        rect.y += rect.height;
        rect.height = fullRect.yMax - rect.y;
      }
      EditableAttributesInspector.OnGUI(rect, new EditableAttributesWrapper(GetEditableAttributes(oldValue, out TypeDeclBuilder newValue)));
      return newValue;
    }

    public static List<EditableAttribute> GetEditableAttributes(TypeDeclBuilder oldValue, out TypeDeclBuilder newValue) {
      if (oldValue == null || oldValue.Fields == null) {
        newValue = new TypeDeclBuilder();
      } else {
        newValue = oldValue;
      }
      var fields = newValue.Fields;
      List<EditableAttribute> attribs = new List<EditableAttribute>();
      attribs.Add(new EditableAttribute {
        Name = $"Field Count",
        Type = typeof(int),
        Getter = node => fields.Count,
        Setter = (node, value) => {
          int count = Mathf.Max(0, value as int? ?? 0);
          while (fields.Count > count) {
            fields.RemoveAt(fields.Count - 1);
          }
          while (fields.Count < count) {
            fields.Add(new TypeDeclBuilderField { Name = $"{fields.Count}" });
          }
        },
      });

      int fieldIndex = 0;
      foreach (TypeDeclBuilderField field in fields) {
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
      return attribs;
    }
  }
}
