using System;
using Unity.VisualScripting;

namespace DNode {
  public class DShift : DArrayOperationBase<DShift.Data> {
    public struct Data {
      public int ShiftAmount;
    }

    [DoNotSerialize] public ValueInput Absolute;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Percent;

    protected override void Definition() {
      base.Definition();
      Absolute = ValueInput<int>(nameof(Absolute), 0);
      Percent = ValueInput<DValue>(nameof(Percent), 0.0);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      int absolute = flow.GetValue<int>(Absolute);
      double percent = flow.GetValue<DValue>(Percent);

      int rows = input.Rows;
      int shiftAmount = absolute + (int)Math.Round(percent * rows);
      data = new Data { ShiftAmount = shiftAmount };
      return (rows, input.Columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int row = 0; row < result.Rows; ++row) {
        result.SetRow(row + data.ShiftAmount, input, row);
      }
    }
  }
}
