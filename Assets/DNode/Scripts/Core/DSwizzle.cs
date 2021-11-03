using Unity.VisualScripting;

namespace DNode {
  public class DSwizzle : DArrayOperationBase<DSwizzle.Data> {
    public struct Data {
      public int[] Indexes;
    }

    [DoNotSerialize] public ValueInput Spec;

    protected override void Definition() {
      base.Definition();
      Spec = ValueInput<string>("Spec", "xyzw");
     }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      string swizzleSpec = flow.GetValue<string>(Spec);
      int[] indexes = CompileSwizzleSpec(swizzleSpec);
      data = new Data { Indexes = indexes };
      return (input.Rows, indexes.Length);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      int[] indexes = data.Indexes;
      for (int row = 0; row < result.Rows; ++row) {
        for (int i = 0; i < indexes.Length; ++i) {
          result[row, i] = input[row, indexes[i]];
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
      return 0;
    }
  }
}
