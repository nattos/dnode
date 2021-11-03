using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DRandom : Unit {
    private static System.Random _random = new System.Random();

    [DoNotSerialize] public ValueInput Rows;
    [DoNotSerialize] public ValueInput Columns;
    [DoNotSerialize][PortLabelHidden][Vector4] public ValueInput Min;
    [DoNotSerialize][PortLabelHidden][Vector4] public ValueInput Max;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Rows = ValueInput<int>(nameof(Rows), 10);
      Columns = ValueInput<int>(nameof(Columns), 1);
      Min = ValueInput<DValue>(nameof(Min), Vector4.zero);
      Max = ValueInput<DValue>(nameof(Max), Vector4.one);

      DValue ComputeFromFlow(Flow flow) {
        int rows = Math.Max(1, flow.GetValue<int>(Rows));
        int cols = Math.Max(1, flow.GetValue<int>(Columns));
        DValue min = flow.GetValue<DValue>(Min);
        DValue max = flow.GetValue<DValue>(Max);
        double[] result = new double[rows * cols];

        for (int row = 0; row < rows; ++row) {
          for (int col = 0; col < cols; ++col) {
            double t = _random.NextDouble();
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
