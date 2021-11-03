using System;

namespace DNode {
  public class DMin : DBinaryScalarOperationBase { protected override double ComputeElement(double lhs, double rhs) => Math.Min(lhs, rhs); }
}