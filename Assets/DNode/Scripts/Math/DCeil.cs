using System;

namespace DNode {
  public class DCeil : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Ceiling(lhs); }
}
