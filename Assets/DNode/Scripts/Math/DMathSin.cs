using System;

namespace DNode {
  public class DMathSin : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Sin(lhs); }
}
