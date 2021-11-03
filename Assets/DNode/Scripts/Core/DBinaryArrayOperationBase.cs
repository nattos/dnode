using Unity.VisualScripting;

namespace DNode {
  public abstract class DBinaryArrayOperationBase<TData> : Unit {
    [DoNotSerialize][NoEditor] public ValueInput A;
    [DoNotSerialize][NoEditor] public ValueInput B;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = ValueInput<DValue>("A", 0);
      B = ValueInput<DValue>("B", 0);

      DValue ComputeFromFlow(Flow flow) {
        DValue a = flow.GetValue<DValue>(A);
        DValue b = flow.GetValue<DValue>(B);
        (int rows, int cols) = GetOutputSize(flow, a, b, out TData data);
        double[] resultArray = new double[rows * cols];
        DMutableValue result = new DMutableValue { ValueArray = resultArray, Columns = cols, Rows = rows };
        FillRows(data, result, a, b);
        return result.ToValue();
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected abstract (int rows, int cols) GetOutputSize(Flow flow, DValue lhs, DValue rhs, out TData data);
    protected abstract void FillRows(TData data, DMutableValue result, DValue lhs, DValue rhs);
  }
}
