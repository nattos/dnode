using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DRange : Unit {
    [DoNotSerialize] public ValueInput Rows;
    [DoNotSerialize] public ValueInput Columns;
    [DoNotSerialize][PortLabelHidden][Scalar] public ValueInput Min;
    [DoNotSerialize][PortLabelHidden][Scalar] public ValueInput Max;
    [DoNotSerialize][PortLabelHidden][Vector4][Range(D3DConstants.DefaultGenericMathMin, D3DConstants.DefaultGenericMathMax, 0.0, 0.0, 0.0, 0.0)] public ValueInput MinVec;
    [DoNotSerialize][PortLabelHidden][Vector4][Range(D3DConstants.DefaultGenericMathMin, D3DConstants.DefaultGenericMathMax, 1.0, 1.0, 1.0, 1.0)] public ValueInput MaxVec;

    private bool _isScalar = true;
    [Serialize][Inspectable] public bool IsScalar {
      get => _isScalar;
      set {
        _isScalar = true;
        PortsChanged();
      }
    }

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Rows = ValueInput<int>(nameof(Rows), 10);
      Columns = ValueInput<int>(nameof(Columns), 1);
      if (_isScalar) {
        Min = ValueInput<DValue>(nameof(Min), 0);
        Max = ValueInput<DValue>(nameof(Max), 1);
      } else {
        MinVec = ValueInput<DValue>(nameof(MinVec), Vector4.zero);
        MaxVec = ValueInput<DValue>(nameof(MaxVec), Vector4.one);
      }

      DValue ComputeFromFlow(Flow flow) {
        int rows = Math.Max(1, flow.GetValue<int>(Rows));
        int cols = Math.Max(1, flow.GetValue<int>(Columns));
        DValue min = _isScalar ? flow.GetValue<DValue>(Min) : flow.GetValue<DValue>(MaxVec);
        DValue max = _isScalar ? flow.GetValue<DValue>(Max) : flow.GetValue<DValue>(MaxVec);
        double[] result = new double[rows * cols];

        for (int row = 0; row < rows; ++row) {
          double t = rows <= 1 ? 0.0 : (row / (double)(rows - 1));
          for (int col = 0; col < cols; ++col) {
            double minValue = min[0, col];
            double maxValue = max[0, col];
            result[row * cols + col] = minValue * (1.0 - t) + maxValue * t;
          }
        }
        return new DValue { ValueArray = result, Columns = cols, Rows = rows };
      }
      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
