using System;
using Unity.VisualScripting;

namespace DNode {
  public class DSplice : DArrayOperationBase<DSplice.Data> {
    public struct Data {
      public DValue ToInsert;
      public int InsertAt;
    }

    [DoNotSerialize][NoEditor] public ValueInput ToInsert;
    [DoNotSerialize] public ValueInput InsertAt;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput InsertAtPercent;

    protected override void Definition() {
      base.Definition();
      ToInsert = ValueInput<DValue>(nameof(ToInsert), 0);
      InsertAt = ValueInput<int>(nameof(InsertAt), 0);
      InsertAtPercent = ValueInput<DValue>(nameof(InsertAtPercent), 0.0);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      DValue toInsert = flow.GetValue<DValue>(ToInsert);
      int insertAtInt = flow.GetValue<int>(InsertAt);
      double insertAtPercent = flow.GetValue<DValue>(InsertAtPercent);
      int insertAt = insertAtInt + (int)Math.Round(input.Rows * insertAtPercent);

      int rows = input.Rows + toInsert.Rows;
      data = new Data { ToInsert = toInsert, InsertAt = insertAt };
      return (rows, input.Columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      int insertAt = data.InsertAt == 0 ? 0 : UnityUtils.Modulo(data.InsertAt - 1, input.Rows) + 1;

      for (int i = 0; i < insertAt; ++i) {
        result.SetRow(i, input, i);
      }
      for (int i = 0; i < data.ToInsert.Rows; ++i) {
        result.SetRow(i + insertAt, data.ToInsert, i);
      }
      for (int i = 0; i < input.Rows - insertAt; ++i) {
        result.SetRow(i + insertAt + data.ToInsert.Rows, input, insertAt + i);
      }
    }
  }
}
