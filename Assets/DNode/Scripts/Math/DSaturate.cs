using System;

namespace DNode {
  public class DSaturate : DUnaryScalarOperationBase { protected override double ComputeElement(double lhs) => Math.Atan(lhs); } // TODO: Moar
}
