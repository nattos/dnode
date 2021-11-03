using System;
using Unity.VisualScripting;

namespace DNode {
  public class D2DInsetRect : DBinaryOperationBase {
    [DoNotSerialize][PortLabelHidden][Vector4][ScreenRectMinMaxRange] public ValueInput MinMax;
    [DoNotSerialize][PortLabelHidden][Vector4][ScreenRectMinMaxRange] public ValueInput Insets;

    protected override string LhsName => nameof(MinMax);
    protected override string RhsName => nameof(Insets);
    protected override ValueInput LhsPort { set { MinMax = value; } }
    protected override ValueInput RhsPort { set { Insets = value; } }

    protected override DValue Compute(DValue minMax, DValue insets) {
      int rows = Math.Max(minMax.Rows, insets.Rows);
      int cols = 4;
      DMutableValue result = new DMutableValue(rows, cols);
      for (int row = 0; row < rows; ++row) {
        result[row, 0] = minMax[row, 0] + insets[row, 0];
        result[row, 1] = minMax[row, 1] + insets[row, 1];
        result[row, 2] = minMax[row, 2] - insets[row, 2];
        result[row, 3] = minMax[row, 3] - insets[row, 3];
      }
      return result.ToValue();
    }
  }
}
