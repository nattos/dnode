namespace DNode {
  public class DLessOrEqual : DBinaryScalarOperationBase { protected override double ComputeElement(double lhs, double rhs) => lhs <= rhs ? 1 : 0; }
}
