using System;

namespace DNode {
  public abstract class DBinaryScalarOperationBase : DBinaryOperationBase {
    protected override DValue Compute(DValue lhs, DValue rhs) {
      int rows = Math.Max(lhs.Rows, rhs.Rows);
      int cols = Math.Max(lhs.Columns, rhs.Columns);
      double[] result = new double[rows * cols];
      for (int row = 0; row < rows; ++row) {
        for (int col = 0; col < cols; ++col) {
          result[row * cols + col] = ComputeElement(lhs[row, col], rhs[row, col]);
        }
      }
      return new DValue { ValueArray = result, Columns = cols, Rows = rows };
    }
  
    protected abstract double ComputeElement(double lhs, double rhs);
  }
}
