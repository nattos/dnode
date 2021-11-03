using System;

namespace DNode {
  public class DWithin : DBinaryScalarOperationBase { protected override double ComputeElement(double lhs, double rhs) => Math.Abs(lhs - rhs) < 0.00001 ? 1 : 0; } // TODO
}
