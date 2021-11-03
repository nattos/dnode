using System;
using Unity.VisualScripting;

namespace DNode {
  public class DRepack2 : DBinaryArrayOperationBase<DRepack2.Data> {
    public struct Data {
      public int[] OutputIndexesA;
      public int[] OutputIndexesB;
    }

    [DoNotSerialize] public ValueInput Columns;
    [DoNotSerialize] public ValueInput OutputSpecA;
    [DoNotSerialize] public ValueInput OutputSpecB;

    protected override void Definition() {
      base.Definition();
      Columns = ValueInput<int>(nameof(Columns), 3);
      OutputSpecA = ValueInput<string>(nameof(OutputSpecA), "xy");
      OutputSpecB = ValueInput<string>(nameof(OutputSpecB), "zw");
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue lhs, DValue rhs, out Data data) {
      int columns = Math.Max(1, flow.GetValue<int>(Columns));
      int[] outputIndexesA = CompileSwizzleSpec(flow.GetValue<string>(OutputSpecA));
      int[] outputIndexesB = CompileSwizzleSpec(flow.GetValue<string>(OutputSpecB));
      data = new Data { OutputIndexesA = outputIndexesA, OutputIndexesB = outputIndexesB };
      return (Math.Max(lhs.Rows, rhs.Rows), columns);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue lhs, DValue rhs) {
      int[] outputIndexesA = data.OutputIndexesA;
      int[] outputIndexesB = data.OutputIndexesB;
      for (int i = 0; i < outputIndexesA.Length; ++i) {
        int outputIndex = outputIndexesA[i];
        if (outputIndex >= 0) {
          for (int row = 0; row < result.Rows; ++row) {
            result[row, outputIndex] = lhs[row, i];
          }
        }
      }
      for (int i = 0; i < outputIndexesB.Length; ++i) {
        int outputIndex = outputIndexesB[i];
        if (outputIndex >= 0) {
          for (int row = 0; row < result.Rows; ++row) {
            result[row, outputIndex] = rhs[row, i];
          }
        }
      }
    }

    private static int[] CompileSwizzleSpec(string spec) {
      int[] indexes = new int[spec.Length];
      for (int i = 0; i < spec.Length; ++i) {
        char c = spec[i];
        indexes[i] = GetCharColumnIndex(c);
      }
      return indexes;
    }

    private static int GetCharColumnIndex(char c) {
      switch (c) {
        case 'x': return 0;
        case 'y': return 1;
        case 'z': return 2;
        case 'w': return 3;
        case 'r': return 0;
        case 'g': return 1;
        case 'b': return 2;
        case 'a': return 3;
        case '0': return 0;
        case '1': return 1;
        case '2': return 2;
        case '3': return 3;
        case '4': return 4;
        case '5': return 5;
        case '6': return 6;
        case '7': return 7;
        case '8': return 8;
        case '9': return 9;
      }
      return -1;
    }
  }
}
