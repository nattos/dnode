using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D2DRectPackMinMax : Unit {
    [DoNotSerialize][PortLabelHidden][Vector2][ScreenRange] public ValueInput Min;
    [DoNotSerialize][PortLabelHidden][Vector2][ScreenRange] public ValueInput Max;
    [DoNotSerialize] public ValueInput Clamp;

    [DoNotSerialize] public ValueOutput result;

    protected override void Definition() {
      Min = ValueInput<DValue>(nameof(Min), -Vector2.one);
      Max = ValueInput<DValue>(nameof(Max), Vector2.one);
      Clamp = ValueInput<bool>(nameof(Clamp), true);

      result = ValueOutput<DValue>("MinMax", DNodeUtils.CachePerFrame(flow => {
        DValue min = flow.GetValue<DValue>(Min);
        DValue max = flow.GetValue<DValue>(Min);
        bool clamp = flow.GetValue<bool>(Clamp);

        int rows = Math.Max(min.Rows, max.Rows);
        int cols = 4;
        DMutableValue result = new DMutableValue(rows, cols);
        for (int row = 0; row < rows; ++row) {
          Vector2 minValue = min.Vector2FromRow(row);
          Vector2 maxValue = max.Vector2FromRow(row);
          if (clamp) {
            maxValue = Vector2.Max(minValue, maxValue);
          }
          result[row, 0] = minValue.x;
          result[row, 1] = minValue.y;
          result[row, 2] = maxValue.x;
          result[row, 3] = maxValue.y;
        }
        return result.ToValue();
      }));
    }
  }
}
