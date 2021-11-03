using System;

namespace DNode {
  public class DMathExp : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Exp(lhs); }
}
