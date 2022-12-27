using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  [IncludeInSettings(true)]
  [TypeIcon(typeof(OnDrawGizmos))]
  public struct DValue : IDisplayDataProvider {
    public double[] ValueArray;

    public int Columns;
    public int Rows;

    public double this[int row, int col] {
      get {
        if (IsEmpty) {
          return 0.0;
        }
        int actualRow = UnityUtils.Modulo(row, Rows);
        int actualCol = UnityUtils.Modulo(col, Columns);
        return ValueArray[actualRow  * Columns + actualCol];
      }
    }

    public bool IsEmpty => Columns <= 0 || Rows <= 0;

    public static implicit operator DValue(double value) {
      return new DValue { ValueArray = new[] { value }, Columns = 1, Rows = 1 };
    }

    public static implicit operator double(DValue value) {
      return value.ValueArray == null ? 0.0 : value.ValueArray[0];
    }

    public static implicit operator DValue(float value) {
      return new DValue { ValueArray = new[] { (double)value }, Columns = 1, Rows = 1 };
    }

    public static implicit operator float(DValue value) {
      return (float)(double)value;
    }

    public static implicit operator DValue(int value) {
      return new DValue { ValueArray = new[] { (double)value }, Columns = 1, Rows = 1 };
    }

    public static implicit operator int(DValue value) {
      return (int)Math.Round((double)value);
    }

    public static implicit operator DValue(double[] value) {
      return new DValue { ValueArray = value, Columns = value.Length, Rows = 1 };
    }

    public static implicit operator double[](DValue value) {
      return value.ValueArray;
    }

    public static implicit operator DValue(Vector2 value) {
      return new DValue { ValueArray = new double[] { value.x, value.y }, Columns = 2, Rows = 1 };
    }

    public static implicit operator Vector2(DValue value) {
      return value.ValueArray == null ? Vector2.zero : value.Vector2FromRow(0);
    }

    public static implicit operator DValue(Vector3 value) {
      return new DValue { ValueArray = new double[] { value.x, value.y, value.z }, Columns = 3, Rows = 1 };
    }

    public static implicit operator Vector3(DValue value) {
      return value.ValueArray == null ? Vector3.zero : value.Vector3FromRow(0);
    }

    public static implicit operator DValue(Vector4 value) {
      return new DValue { ValueArray = new double[] { value.x, value.y, value.z, value.w }, Columns = 4, Rows = 1 };
    }

    public static implicit operator Vector4(DValue value) {
      return value.ValueArray == null ? Vector4.zero : value.Vector4FromRow(0);
    }

    public static implicit operator DValue(Color value) {
      return new DValue { ValueArray = new double[] { value.r, value.g, value.b, value.a }, Columns = 4, Rows = 1 };
    }

    public static implicit operator Color(DValue value) {
      return value.ValueArray == null ? Color.clear : value.ColorFromRow(0);
    }

    public static implicit operator DValue(Quaternion value) {
      return new DValue { ValueArray = new double[] { value.x, value.y, value.z, value.w }, Columns = 4, Rows = 1 };
    }

    public static implicit operator Quaternion(DValue value) {
      return value.ValueArray == null ? Quaternion.identity : value.QuaternionFromRow(0);
    }
    
    public float FloatFromRow(int row, Vector2 defaultValues = default) {
      return (float)this[row, 0];
    }

    public bool BoolFromRow(int row, Vector2 defaultValues = default) {
      return this[row, 0] != 0.0;
    }

    public Vector2 Vector2FromRow(int row, Vector2 defaultValues = default) {
      if (Columns == 1) {
        float value = (float)this[row, 0];
        return new Vector3(value, value, value);
      }

      Vector3 result = defaultValues;
      int cols = Math.Min(2, Columns);
      for (int col = 0; col < cols; ++col) {
        result[col] = (float)this[row, col];
      }
      return result;
    }

    public Vector3 Vector3FromRow(int row, Vector3 defaultValues = default) {
      if (Columns == 1) {
        float value = (float)this[row, 0];
        return new Vector3(value, value, value);
      }

      Vector3 result = defaultValues;
      int cols = Math.Min(3, Columns);
      for (int col = 0; col < cols; ++col) {
        result[col] = (float)this[row, col];
      }
      return result;
    }

    public Quaternion QuaternionEulerFromRow(int row, Vector3 defaultValues = default) {
      return Quaternion.Euler(Vector3FromRow(row, defaultValues));
    }

    public Vector4 Vector4FromRow(int row, Vector4 defaultValues = default) {
      if (Columns == 1) {
        float value = (float)this[row, 0];
        return new Vector4(value, value, value, value);
      }

      Vector4 result = defaultValues;
      int cols = Math.Min(4, Columns);
      for (int col = 0; col < cols; ++col) {
        result[col] = (float)this[row, col];
      }
      return result;
    }
  
    public Color ColorFromRow(int row, Color defaultValues = default) {
      if (Columns == 1) {
        float value = (float)this[row, 0];
        return new Color(value, value, value, value);
      }

      Color result = defaultValues;
      int cols = Math.Min(4, Columns);
      for (int col = 0; col < cols; ++col) {
        result[col] = (float)this[row, col];
      }
      return result;
    }
    
    public Quaternion QuaternionFromRow(int row) {
      return new Quaternion((float)this[row, 0], (float)this[row, 1], (float)this[row, 2], (float)this[row, 3]);
    }

    public override string ToString() {
      return IsEmpty ? "<empty>" : $"[ {string.Join(", ", ValueArray.Take(12).Select(v => v.ToString("G3"))) + (ValueArray.Length > 12 ? "..." : "")} ]";
    }

    public string ToShortString() {
      string prefix = ValueArray?.Length > 1 ? "[ " : "";
      string suffix = ValueArray?.Length > 1 ? " ]" : "";
      return IsEmpty ? "<empty>" : $"{prefix}{string.Join(", ", ValueArray.Take(3).Select(v => v.ToString("0.##").Replace("Infinity", "Inf"))) + (ValueArray.Length > 3 ? "..." : "")}{suffix}";
    }
    
    public Type DisplayIconAsType {
      get {
        if (Rows == 1) {
          if (Columns == 1) {
            return typeof(double);
          } else if (Columns == 2) {
            return typeof(Vector2);
          } else if (Columns == 3) {
            return typeof(Vector3);
          } else if (Columns == 4) {
            return typeof(Vector4);
          }
        }
        return null;
      }
    }

    public static void Init() {
      // !!!ON NO HACK!!!
      System.Reflection.FieldInfo typesWithDefaultValuesField = typeof(ValueInput).GetField("typesWithDefaultValues", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
      ICollection<Type> typesWithDefaultValues = (ICollection<Type>)typesWithDefaultValuesField.GetValue(null);
      typesWithDefaultValues.Add(typeof(DValue));
      typesWithDefaultValues.Add(typeof(DCustomInspectorValue));
      typesWithDefaultValues.Add(typeof(DEvent));
      // typesWithDefaultValues.Add(typeof(DFontSpec));
      // typesWithDefaultValues.Add(typeof(DIOFrameInputAddressSpec));
      // typesWithDefaultValues.Add(typeof(DLaunchableTriggerValue));
    }

    static DValue() {
      Init();
    }
  }
}
