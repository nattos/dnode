using System;

namespace DNode {
  public class DMathRad2Deg : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => lhs * 360 / (Math.PI * 2); }
}
