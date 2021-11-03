using System;

namespace DNode {
  public class DClamp : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Max(0.0f, Math.Min(1.0, lhs)); } // TODO: Moar
}
