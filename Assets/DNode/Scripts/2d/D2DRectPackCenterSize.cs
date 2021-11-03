using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D2DRectPackCenterSize : Unit {
    [DoNotSerialize][PortLabelHidden][Vector2][ScreenRange] public ValueInput Center;
    [DoNotSerialize][PortLabelHidden][Vector2][ScreenRange] public ValueInput Size;
    [DoNotSerialize] public ValueInput Clamp;

    [DoNotSerialize] public ValueOutput result;

    protected override void Definition() {
      Center = ValueInput<DValue>(nameof(Center), 0.0);
      Size = ValueInput<DValue>(nameof(Size), Vector2.one);
      Clamp = ValueInput<bool>(nameof(Clamp), true);

      result = ValueOutput<DValue>("MinMax", DNodeUtils.CachePerFrame(flow => {
        DValue center = flow.GetValue<DValue>(Center);
        DValue size = flow.GetValue<DValue>(Center);
        bool clamp = flow.GetValue<bool>(Clamp);

        int rows = Math.Max(center.Rows, size.Rows);
        int cols = 4;
        DMutableValue result = new DMutableValue(rows, cols);
        for (int row = 0; row < rows; ++row) {
          Vector2 centerValue = center.Vector2FromRow(row);
          Vector2 sizeValue = size.Vector2FromRow(row);
          if (clamp) {
            sizeValue = Vector2.Max(sizeValue, Vector2.zero);
          }
          Vector2 minValue = centerValue - sizeValue * 0.5f;
          Vector2 maxValue = centerValue + sizeValue * 0.5f;
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
