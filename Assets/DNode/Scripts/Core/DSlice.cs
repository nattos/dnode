using System;
using Unity.VisualScripting;

namespace DNode {
  public class DSlice : DArrayOperationBase<DSlice.Data> {
    public struct Data {
      public int Start;
      public int End;
      public int Count;
      public int Step;
    }

    [DoNotSerialize] public ValueInput Start;
    [DoNotSerialize] public ValueInput End;
    [DoNotSerialize] public ValueInput Step;

    protected override void Definition() {
      base.Definition();
      Start = ValueInput<int>("Start", 0);
      End = ValueInput<int>("End", 0);
      Step = ValueInput<int>("Step", 1);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      int start = flow.GetValue<int>(Start);
      int end = flow.GetValue<int>(End);
      int step = Math.Max(1, flow.GetValue<int>(Step));
      int delta = end - start;
      int count = (Math.Abs(delta) + 1) / step;
      if (delta < 0) {
        step = -step;
      }
      data = new Data { Start = start, End = end, Count = count, Step = step };
      return (count, input.Columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      int row = data.Start;
      for (int i = 0; i < data.Count; ++i) {
        result.SetRow(i, input, row);
        row += data.Step;
      }
    }
  }
}
