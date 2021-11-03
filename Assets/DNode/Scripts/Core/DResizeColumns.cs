using System;
using Unity.VisualScripting;

namespace DNode {
  public class DResizeColumns : DArrayOperationBase<DResizeColumns.Data> {
    public struct Data {}

    [DoNotSerialize] public ValueInput Columns;

    protected override void Definition() {
      base.Definition();
      Columns = ValueInput<int>("Columns", 1);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = default;
      int columns = Math.Max(1, flow.GetValue<int>(Columns));
      return (input.Rows, columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int row = 0; row < result.Rows; ++row) {
        result.SetRow(row, input, row);
      }
    }
  }
}
