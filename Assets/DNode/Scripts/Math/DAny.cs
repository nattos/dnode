namespace DNode {
  public class DAny : DAggregateOperationBase {
    protected override double InitialValue(DValue lhs) => 0;
    protected override double AggregateElement(double acc, double value) => (acc != 0 || value != 0) ? 1 : 0;
  }
}
