using System;
using System.Linq;
using UnityEngine;
using Unity.VisualScripting;

namespace DNode {
  public class DLiteral : Unit, IDCustomInspectorDataProvider {
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
    private bool _isLogScale = false;
    private double _logScalingFactor = 1.0;
    private ClampMode _clampMode = ClampMode.Clamp;
    [Serialize][Inspectable] public Vector4 DefaultValue {
      get => _valueDefault;
      set {
        _valueDefault = value;
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
    [Serialize][Inspectable] public bool IsLogScale {
      get => _isLogScale;
      set {
        _isLogScale = value;
        PortsChanged();
      }
    }
    [Serialize][Inspectable] public double LogScalingFactor {
      get => _logScalingFactor;
      set {
        _logScalingFactor = value;
        PortsChanged();
      }
    }
    [Serialize][Inspectable] public ClampMode ClampMode {
      get => _clampMode;
      set {
        _clampMode = value;
        PortsChanged();
      }
    }

    private int _resultLength;

    private DCustomInspectorValue MakeValue(int axis) {
      return new DCustomInspectorValue {
        Value = _valueDefault[axis],
        Key = $"{axis}",
      };
    }

    public DCustomInspectorData? ProvideCustomInspectorData(string key) {
      int axis;
      switch (key) {
        case "0": axis = 0; break;
        case "1": axis = 1; break;
        case "2": axis = 2; break;
        case "3": axis = 3; break;
        default:
          return null;
      }
      return new DCustomInspectorData {
        MinValue = _valueMin[axis],
        MaxValue = _valueMax[axis],
        DefaultValue = _valueDefault[axis],
        IsLogScale = _isLogScale,
        LogScalingFactor = _logScalingFactor,
        ClampMode = _clampMode,
      };
    }

    protected override void Definition() {
      X = ValueInput<DCustomInspectorValue>(nameof(X), MakeValue(0));
      _resultLength = 1;
      if (_vectorLength >= 2) {
        Y = ValueInput<DCustomInspectorValue>(nameof(Y), MakeValue(1));
        _resultLength = 2;
      }
      if (_vectorLength >= 3) {
        Z = ValueInput<DCustomInspectorValue>(nameof(Z), MakeValue(2));
        _resultLength = 3;
      }
      if (_vectorLength >= 4) {
        W = ValueInput<DCustomInspectorValue>(nameof(W), MakeValue(3));
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
