using System;
using System.Linq;
using UnityEngine;
using Unity.VisualScripting;

namespace DNode {
  public class DLiteral : Unit {
    [DoNotSerialize][PortLabelHidden][ShortEditor] public ValueInput X;
    [DoNotSerialize][PortLabelHidden][ShortEditor] public ValueInput Y;
    [DoNotSerialize][PortLabelHidden][ShortEditor] public ValueInput Z;
    [DoNotSerialize][PortLabelHidden][ShortEditor] public ValueInput W;
    [DoNotSerialize][PortLabelHidden] public ValueOutput result;

    private int _vectorLength = 1;
    [Serialize][Inspectable][InspectorRange(1, 4)] public int VectorLength {
      get => _vectorLength;
      set {
        _vectorLength = value;
        PortsChanged();
      }
    }

    private Vector4 _valueDefault = Vector4.zero;
    private Vector4 _valueMin = Vector4.zero;
    private Vector4 _valueMax = Vector4.one;
    [Serialize][Inspectable] public Vector4 DefaultValue {
      get => _valueMin;
      set {
        _valueMin = value;
        PortsChanged();
      }
    }
    [Serialize][Inspectable] public Vector4 MinValue {
      get => _valueMin;
      set {
        _valueMin = value;
        PortsChanged();
      }
    }
    [Serialize][Inspectable] public Vector4 MaxValue {
      get => _valueMax;
      set {
        _valueMax = value;
        PortsChanged();
      }
    }

    private int _resultLength;

    protected override void Definition() {
      X = ValueInput<DValue>(nameof(X), _valueDefault.x);
      _resultLength = 1;
      if (_vectorLength >= 2) {
        Y = ValueInput<DValue>(nameof(Y), _valueDefault.y);
        _resultLength = 2;
      }
      if (_vectorLength >= 3) {
        Z = ValueInput<DValue>(nameof(Z), _valueDefault.z);
        _resultLength = 3;
      }
      if (_vectorLength >= 4) {
        W = ValueInput<DValue>(nameof(W), _valueDefault.w);
        _resultLength = 4;
      }

      DValue ComputeFromFlow(Flow flow) {
        DMutableValue result = new DMutableValue(1, _resultLength);
        result[0, 0] = (double)flow.GetValue<DValue>(X);
        if (_vectorLength >= 2) {
          result[0, 1] = (double)flow.GetValue<DValue>(Y);
        }
        if (_vectorLength >= 3) {
          result[0, 2] = (double)flow.GetValue<DValue>(Z);
        }
        if (_vectorLength >= 4) {
          result[0, 3] = (double)flow.GetValue<DValue>(W);
        }
        return result.ToValue();
      }
      result = ValueOutput<DValue>(nameof(result), DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
