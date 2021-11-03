using System;

namespace DNode {
  public class DPower : DBinaryScalarOperationBase { protected override double ComputeElement(double lhs, double rhs) => Math.Pow(lhs, rhs); }
}
