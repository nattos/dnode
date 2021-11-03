using System;

namespace DNode {
  public class DRound : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Round(lhs); }
}
