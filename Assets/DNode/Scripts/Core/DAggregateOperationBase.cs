using Unity.VisualScripting;

namespace DNode {
  public abstract class DAggregateOperationBase : Unit {
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueInput A;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = ValueInput<DValue>("A", 0);

      double ComputeFromFlow(Flow flow) {
        return Aggregate(flow.GetValue<DValue>(A));
      }

      result = ValueOutput<double>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    private double Aggregate(DValue lhs) {
      int rows = lhs.Rows;
      int cols = lhs.Columns;
      double acc = InitialValue(lhs);
      for (int row = 0; row < rows; ++row) {
        for (int col = 0; col < cols; ++col) {
          acc = AggregateElement(acc, lhs[row, col]);
        }
      }
      return acc;
    }

    protected abstract double InitialValue(DValue lhs);
    protected abstract double AggregateElement(double acc, double value);
  }
}
