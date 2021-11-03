namespace DNode {
  public class DLess : DBinaryScalarOperationBase { protected override double ComputeElement(double lhs, double rhs) => lhs < rhs ? 1 : 0; }
}
