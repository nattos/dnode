using Unity.VisualScripting;

namespace DNode {
  public class DTranspose : DArrayOperationBase<DTranspose.Data> {
    public struct Data {}

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      return (input.Columns, input.Rows);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int row = 0; row < result.Rows; ++row) {
        for (int col = 0; col < result.Columns; ++col) {
          result[row, col] = input[col, row];
        }
      }
    }
  }
}
