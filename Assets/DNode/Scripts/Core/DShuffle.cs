using System;
using Unity.VisualScripting;

namespace DNode {
  public class DShuffle : DArrayOperationBase<DShuffle.Data> {
    private static System.Random _random = new System.Random();

    public struct Data {
      public DValue PValues;
    }

    [DoNotSerialize][NoEditor] public ValueInput PValues;

    protected override void Definition() {
      base.Definition();
      PValues = ValueInput<DValue>(nameof(PValues), default);
     }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      DValue pValues = flow.GetValue<DValue>(PValues);

      int rows = pValues.IsEmpty ? input.Rows : pValues.Rows;

      data = new Data { PValues = pValues };
      return (rows, input.Columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      int rows = result.Rows;
      for (int row = 0; row < rows; ++row) {
        result.SetRow(row, input, row);
      }

      for (int row = 0; row < rows; ++row) {
        double pValue = data.PValues.IsEmpty ? _random.NextDouble() : data.PValues[row, 0];
        int targetRow = Math.Max(0, Math.Min(rows - 1, (int)Math.Truncate(rows * pValue)));
        result.SwapRow(row, targetRow);
      }
    }
  }
}
