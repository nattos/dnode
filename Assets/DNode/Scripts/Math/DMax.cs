using System;

namespace DNode {
  public class DMax : DBinaryScalarOperationBase { protected override double ComputeElement(double lhs, double rhs) => Math.Max(lhs, rhs); }
}
