using System;
using Unity.VisualScripting;

namespace DNode {
  public class DResize : DArrayOperationBase<DResize.Data> {
    public struct Data {}

    [DoNotSerialize] public ValueInput Size;

    protected override void Definition() {
      base.Definition();
      Size = ValueInput<int>("Size", 1);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = default;
      int size = Math.Max(1, flow.GetValue<int>(Size));
      return (size, input.Columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int row = 0; row < result.Rows; ++row) {
        result.SetRow(row, input, row);
      }
    }
  }
}
