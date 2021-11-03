using System;

namespace DNode {
  public class DFloor : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Floor(lhs); }
}
