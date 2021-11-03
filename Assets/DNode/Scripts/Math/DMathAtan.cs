using System;

namespace DNode {
  public class DMathAtan : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Atan(lhs); }
}
