using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public struct DCustomInspectorData {
    public double? MinValue;
    public double? MaxValue;
    public DValue? DefaultValue;
    public bool? IsLogScale;
    public double? LogScalingFactor;
    public ClampMode? ClampMode;
    public int? Dimensions;
  }

  public interface IDCustomInspectorDataProvider {
    DCustomInspectorData? ProvideCustomInspectorData(string key);
  }

  public interface IValueEditedHandler {
    void OnValueEdited(string fieldName);
  }

  [TypeIcon(typeof(OnDrawGizmos))]
  public struct DCustomInspectorValue : IDisplayDataProvider {
    public DValue Value;
    public string Key;

    public static implicit operator DCustomInspectorValue(DValue value) {
      return new DCustomInspectorValue { Value = value };
    }

    public static implicit operator DValue(DCustomInspectorValue value) {
      return value.Value;
    }

    public override string ToString() {
      return Value.ToString();
    }

    public string ToShortString() {
      return Value.ToShortString();
    }

    public Type DisplayIconAsType => Value.DisplayIconAsType;
  }
}
