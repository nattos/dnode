namespace DNode {
  public class DAll : DAggregateOperationBase {
    protected override double InitialValue(DValue lhs) => 1;
    protected override double AggregateElement(double acc, double value) => (acc != 0 && value != 0) ? 1 : 0;
  }
}
