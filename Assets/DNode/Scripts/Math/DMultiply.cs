namespace DNode {
  public class DMultiply : DBinaryScalarOperationBase {
    protected override double RhsDefaultValue => 1.0;
    protected override double ComputeElement(double lhs, double rhs) => lhs * rhs;
  }
}