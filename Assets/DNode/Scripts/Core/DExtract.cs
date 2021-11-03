using System;
using Unity.VisualScripting;

namespace DNode {
  public class DExtract : DArrayOperationBase<DExtract.Data> {
    public struct Data {
      public DValue Indexes;
      public bool Interpolate;
    }

    [DoNotSerialize][NoEditor] public ValueInput Indexes;
    [DoNotSerialize] public ValueInput Interpolate;

    protected override void Definition() {
      base.Definition();
      Indexes = ValueInput<DValue>("Indexes", 0);
      Interpolate = ValueInput<bool>("Interpolate", false);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      DValue indexes = flow.GetValue<DValue>(Indexes);
      bool interpolate = flow.GetValue<bool>(Interpolate);

      int rows = indexes.Rows;
      data = new Data { Indexes = indexes, Interpolate = interpolate };
      return (rows, input.Columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      DValue indexes = data.Indexes;
      if (!data.Interpolate) {
        for (int i = 0; i < indexes.Rows; ++i) {
          result.SetRow(i, input, (int)Math.Truncate(indexes[i, 0]));
        }
      } else {
        int columns = result.Columns;
        for (int i = 0; i < indexes.Rows; ++i) {
          double rawIndex = indexes[i, 0];
          int coarseIndex = (int)Math.Floor(rawIndex);
          double fineIndex = rawIndex - coarseIndex;

          for (int col = 0; col < columns; ++col) {
            double sample1 = input[coarseIndex, col];
            double sample2 = input[coarseIndex + 1, col];
            result[i, col] = sample1 * (1 - fineIndex) + sample2 * fineIndex;
          }
        }
      }
    }
  }
}
