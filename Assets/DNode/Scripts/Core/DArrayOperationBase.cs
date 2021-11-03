using Unity.VisualScripting;

namespace DNode {
  public abstract class DArrayOperationBase<TData> : Unit {
    [DoNotSerialize][PortLabelHidden][NoEditor] public ValueInput Input;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Input = ValueInput<DValue>(nameof(Input), 0);

      DValue ComputeFromFlow(Flow flow) {
        DValue input = flow.GetValue<DValue>(Input);
        (int rows, int cols) = GetOutputSize(flow, input, out TData data);
        double[] resultArray = new double[rows * cols];
        DMutableValue result = new DMutableValue { ValueArray = resultArray, Columns = cols, Rows = rows };
        FillRows(data, result, input);
        return result.ToValue();
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected abstract (int rows, int cols) GetOutputSize(Flow flow, DValue input, out TData data);
    protected abstract void FillRows(TData data, DMutableValue result, DValue input);
  }
}
