using System.Linq;

namespace DNode {
  public struct DMutableValue {
    public double[] ValueArray;

    public int Columns;
    public int Rows;

    public DMutableValue(int rows, int columns) {
      ValueArray = new double[rows * columns];
      Columns = columns;
      Rows = rows;
    }

    public double this[int row, int col] {
      get {
        int actualRow = UnityUtils.Modulo(row, Rows);
        int actualCol = UnityUtils.Modulo(col, Columns);
        return ValueArray[actualRow  * Columns + actualCol];
      }
      set {
        int actualRow = UnityUtils.Modulo(row, Rows);
        int actualCol = UnityUtils.Modulo(col, Columns);
        ValueArray[actualRow  * Columns + actualCol] = value;
      }
    }

    public void SetRow(int destRow, DValue other, int srcRow) {
      for (int col = 0; col < Columns; ++col) {
        this[destRow, col] = other[srcRow, col];
      }
    }

    public void SwapRow(int rowA, int rowB) {
      for (int col = 0; col < Columns; ++col) {
        double tmp = this[rowA, col];
        this[rowA, col] = this[rowB, col];
        this[rowB, col] = tmp;
      }
    }

    public DValue ToValue() => new DValue { ValueArray = ValueArray, Columns = Columns, Rows = Rows };

    public override string ToString() {
      return $"[ {string.Join(", ", ValueArray.Select(v => v.ToString("G3")))} ]";
    }
  }
}
