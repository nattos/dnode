using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace NanoGraph {
  public static class EditableAttributesInspector {
    public static float GetHeight(float width, IEditableAttributeProvider attributesProvider) {
      IReadOnlyList<EditableAttribute> editableAttributes = attributesProvider?.EditableAttributes ?? Array.Empty<EditableAttribute>();
      float height = 0.0f;
      foreach (var attrib in editableAttributes) {
        float attribHeight = EditorGUIUtility.singleLineHeight;
        if (attrib.Type == typeof(TypeDeclBuilder)) {
          attribHeight = TypeDeclBuilderInspector.GetHeight(width, attrib.Name, attrib.Getter.Invoke(attributesProvider) as TypeDeclBuilder);
        }
        height += attribHeight;
      }
      return height;
    }

    public static void OnGUI(Rect position, IEditableAttributeProvider attributesProvider) {
      Rect rect = position;

      IReadOnlyList<EditableAttribute> editableAttributes = attributesProvider?.EditableAttributes ?? Array.Empty<EditableAttribute>();
      foreach (var attrib in editableAttributes) {
        rect.height = EditorGUIUtility.singleLineHeight;
        object resultValue = null;
        using (var check = new EditorGUI.ChangeCheckScope()) {
          if (attrib.Type == typeof(string)) {
            resultValue = EditorGUI.TextField(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as string ?? "");
          } else if (attrib.Type == typeof(int)) {
            resultValue = EditorGUI.IntField(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as int? ?? 0);
          } else if (attrib.Type == typeof(float)) {
            resultValue = EditorGUI.FloatField(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as float? ?? 0.0f);
          } else if (attrib.Type == typeof(double)) {
            resultValue = (double)EditorGUI.FloatField(rect, attrib.Name, (float)(attrib.Getter.Invoke(attributesProvider) as double? ?? 0.0));
          } else if (attrib.Type == typeof(bool)) {
            resultValue = EditorGUI.Toggle(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as bool? ?? false);
          } else if (attrib.Type.IsEnum) {
            resultValue = EditorGUI.EnumPopup(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as Enum);
          } else if (attrib.Type == typeof(TypeDeclBuilder)) {
            // Performance could be improved.
            rect.height = TypeDeclBuilderInspector.GetHeight(rect.width, attrib.Name, attrib.Getter.Invoke(attributesProvider) as TypeDeclBuilder);
            resultValue = TypeDeclBuilderInspector.OnGUI(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as TypeDeclBuilder);
          } else if (typeof(UnityEngine.Object).IsAssignableFrom(attrib.Type)) {
            resultValue = EditorGUI.ObjectField(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as UnityEngine.Object, attrib.Type, allowSceneObjects: false);
          }
          if (check.changed) {
            attrib.Setter.Invoke(attributesProvider, resultValue);
          }
        }
        rect.y += rect.height;
      }
    }
  }
}
