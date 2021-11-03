using System;

namespace DNode {
  public class DMathCos : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Cos(lhs); }
}
