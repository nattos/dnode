namespace DNode {
  public abstract class DUnaryScalarOperationBase : DUnaryOperationBase {
    protected override DValue Compute(DValue lhs) {
      int rows = lhs.Rows;
      int cols = lhs.Columns;
      double[] result = new double[rows * cols];
      for (int row = 0; row < rows; ++row) {
        for (int col = 0; col < cols; ++col) {
          result[row * cols + col] = ComputeElement(lhs[row, col]);
        }
      }
      return new DValue { ValueArray = result, Columns = cols, Rows = rows };
    }
  
    protected abstract double ComputeElement(double lhs);
  }
}
