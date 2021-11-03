using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  [TypeIcon(typeof(OnDrawGizmosSelected))]
  public struct DEvent : IDisplayDataProvider {
    public Func<DValue> ValueProvider;
    public DValue ImmediateValue;

    public bool IsTriggered;
    public DValue Value => ValueProvider?.Invoke() ?? ImmediateValue;
    public DValue? OptionalValue => IsTriggered ? (DValue?)Value : null;

    public static implicit operator DEvent(DValue value) {
      return CreateImmediate(value, triggered: true);
    }

    public static implicit operator DValue(DEvent value) {
      return value.Value;
    }

    public static implicit operator bool(DEvent value) {
      return value.IsTriggered;
    }

    public static implicit operator DEvent(double value) => (DEvent)(DValue)value;
    public static implicit operator double(DEvent value) => (DValue)value;
    public static implicit operator DEvent(float value) => (DEvent)(DValue)value;
    public static implicit operator float(DEvent value) => (DValue)value;
    public static implicit operator float?(DEvent value) => (DValue)value;
    public static implicit operator DEvent(double[] value) => (DEvent)(DValue)value;
    public static implicit operator double[](DEvent value) => (DValue)value;
    public static implicit operator DEvent(Vector2 value) => (DEvent)(DValue)value;
    public static implicit operator Vector2(DEvent value) => (DValue)value;
    public static implicit operator DEvent(Vector3 value) => (DEvent)(DValue)value;
    public static implicit operator Vector3(DEvent value) => (DValue)value;
    public static implicit operator DEvent(Vector4 value) => (DEvent)(DValue)value;
    public static implicit operator Vector4(DEvent value) => (DValue)value;
    public static implicit operator DEvent(Color value) => (DEvent)(DValue)value;
    public static implicit operator Color(DEvent value) => (DValue)value;
    public static implicit operator DEvent(Quaternion value) => (DEvent)(DValue)value;
    public static implicit operator Quaternion(DEvent value) => (DValue)value;

    public static DEvent Create(Func<DValue> valueProvider, bool triggered) {
      if (triggered) {
        return CreateImmediate(valueProvider.Invoke(), triggered: true);
      }
      return new DEvent { ValueProvider = valueProvider, IsTriggered = false };
    }

    public static DEvent CreateImmediate(DValue value, bool triggered) {
      return new DEvent { ImmediateValue = value, IsTriggered = triggered };
    }

    public static DEvent GetOptionalEventInput(Unity.VisualScripting.Flow flow, Unity.VisualScripting.ValueInput input) {
      if (!input.connections.Any()) {
        return DEvent.Create(() => default, triggered: false);
      }
      return flow.GetValue<DEvent>(input);
    }

    public string ToShortString() {
      if (IsTriggered) {
        return $"<true>{(ValueProvider != null ? "" : $" {ImmediateValue.ToShortString()}")}";
      } else {
        return ValueProvider != null ? "" : $"{ImmediateValue.ToShortString()}";
      }
    }

    public Type DisplayIconAsType => ValueProvider != null ? null : ImmediateValue.DisplayIconAsType;

    static DEvent() {
      DValue.Init();
    }
  }
}
