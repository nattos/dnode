namespace DNode {
  public class DNot : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => lhs == 0 ? 1 : 0; }
}