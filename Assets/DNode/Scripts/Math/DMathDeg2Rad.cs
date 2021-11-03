using System;

namespace DNode {
  public class DMathDeg2Rad : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => lhs / 360 * Math.PI * 2; }
}
