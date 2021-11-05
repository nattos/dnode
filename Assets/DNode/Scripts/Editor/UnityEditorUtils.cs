using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEditor;

namespace DNode {
  public static class UnityEditorUtils {
    public static bool ColoredButton(Rect rect, string label, GUIStyle style, Color backgroundColor) {
      Color oldColor = GUI.backgroundColor;
      GUI.backgroundColor = backgroundColor;
      bool result = GUI.Button(rect, label, style);
      GUI.backgroundColor = oldColor;
      return result;
    }
  
    public static bool ColoredButton(Rect rect, GUIContent content, GUIStyle style, Color backgroundColor) {
      Color oldColor = GUI.backgroundColor;
      GUI.backgroundColor = backgroundColor;
      bool result = GUI.Button(rect, content, style);
      GUI.backgroundColor = oldColor;
      return result;
    }

    public static Rect Inset(Rect rect, float inset) {
      rect.x += inset;
      rect.y += inset;
      rect.width -= inset * 2.0f;
      rect.height -= inset * 2.0f;
      return rect;
    }

    public static Rect Inset(Rect rect, float insetX, float insetY) {
      rect.x += insetX;
      rect.y += insetY;
      rect.width -= insetX * 2.0f;
      rect.height -= insetY * 2.0f;
      return rect;
    }

    public static Rect PixelAlignRect(Rect rect) {
      // TODO: Doesn't work. Seems to be some sort of scaling factor applied.
      rect.x = Mathf.Round(rect.x);
      rect.y = Mathf.Round(rect.y);
      rect.width = Mathf.Round(rect.width);
      rect.height = Mathf.Round(rect.height);
      return rect;
    }

    public static string GetFieldLabel(Metadata metadata) {
      if (metadata.parent.parent.HasAttribute<LabelAttribute>()) {
        var attribute = metadata.parent.parent.GetAttribute<LabelAttribute>();
        return attribute.Label;
      }
      return metadata.parent.parent.label?.text;
    }

    public static bool TryGetAttribute<T>(Metadata metadata, out T attrib) where T : Attribute {
      if (metadata.parent?.parent?.HasAttribute<T>() == true) {
        attrib = metadata.parent.parent.GetAttribute<T>();
        return true;
      }
      if (metadata.HasAttribute<T>()) {
        attrib = metadata.GetAttribute<T>();
        return true;
      }
      attrib = null;
      return false;
    }

    public static bool IsFieldEditable(Metadata metadata) {
      return metadata.parent.parent.isEditable;
    }

    public static Func<TResult> RateLimitedFunc<TResult>(float minDelaySeconds, Func<TResult> func) {
      double lastCalledTime = float.MinValue;
      TResult cachedValue = default;
      return () => {
        double time = EditorApplication.timeSinceStartup;
        if (time - lastCalledTime > minDelaySeconds) {
          lastCalledTime = time;
          cachedValue = func.Invoke();
        }
        return cachedValue;
      };
    }

    public static Func<TData, TResult> RateLimitedFunc<TData, TResult>(float minDelaySeconds, Func<TData, TResult> func) {
      double lastCalledTime = float.MinValue;
      TResult cachedValue = default;
      return data => {
        double time = EditorApplication.timeSinceStartup;
        if (time - lastCalledTime > minDelaySeconds) {
          lastCalledTime = time;
          cachedValue = func.Invoke(data);
        }
        return cachedValue;
      };
    }
  }
}
