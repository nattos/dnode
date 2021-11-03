using Unity.VisualScripting;

namespace DNode {
  public abstract class DUnaryDataScalarOperationBase<TData> : Unit {
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueInput A;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = ValueInput<DValue>("A", 0);

      DValue ComputeFromFlow(Flow flow) {
        DValue lhs = flow.GetValue<DValue>(A);
        TData data = GetData(flow, lhs);
        return Compute(data, lhs);
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    private DValue Compute(TData data, DValue lhs) {
      int rows = lhs.Rows;
      int cols = lhs.Columns;
      double[] result = new double[rows * cols];
      for (int row = 0; row < rows; ++row) {
        for (int col = 0; col < cols; ++col) {
          result[row * cols + col] = ComputeElement(data, lhs[row, col]);
        }
      }
      return new DValue { ValueArray = result, Columns = cols, Rows = rows };
    }
  
    protected abstract TData GetData(Flow flow, DValue input);
    protected abstract double ComputeElement(TData data, double lhs);
  }
}
