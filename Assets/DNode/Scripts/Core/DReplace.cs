using System;
using Unity.VisualScripting;

namespace DNode {
  public class DReplace : DArrayOperationBase<DReplace.Data> {
    public struct Data {
      public DValue Values;
      public (int inputIndex, int outputIndex)[] Indexes;
    }

    [DoNotSerialize] public ValueInput Values;
    [DoNotSerialize] public ValueInput Indexes;

    protected override void Definition() {
      base.Definition();
      Values = ValueInput<DValue>("Values");
      Indexes = ValueInput<DValue>("Indexes");
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      DValue values = flow.GetValue<DValue>(Values);
      DValue rawIndexes = flow.GetValue<DValue>(Indexes);
      int indexCount = Math.Min(values.Rows, rawIndexes.Rows);
      (int inputIndex, int outputIndex)[] indexes = new (int, int)[indexCount];
      for (int i = 0; i < indexCount; ++i) {
        indexes[i] = (i, (int)Math.Round(rawIndexes[i, 0]));
      }
      Array.Sort(indexes, (a, b) => a.outputIndex - b.outputIndex);

      data = new Data { Values = values, Indexes = indexes };
      return (input.Rows, input.Columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      int index = 0;
      int nextReplaceIndex = data.Indexes[index].outputIndex;
      for (int i = 0; i < result.Rows; ++i) {
        if (nextReplaceIndex == i) {
          while (nextReplaceIndex == i) {
            ++index;
            if (index >= data.Indexes.Length) {
              break;
            }
            nextReplaceIndex = data.Indexes[index].outputIndex;
          }
          result.SetRow(i, data.Values, data.Indexes[index - 1].inputIndex);
        } else {
          result.SetRow(i, input, i);
        }
      }
    }
  }
}
