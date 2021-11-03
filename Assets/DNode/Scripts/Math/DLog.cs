using System;

namespace DNode {
  public class DLog : DBinaryScalarOperationBase { protected override double ComputeElement(double lhs, double rhs) => Math.Log(lhs, rhs); }
}
