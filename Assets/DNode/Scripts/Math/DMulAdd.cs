using Unity.VisualScripting;

namespace DNode {
  public class DMultiplyAdd : DTernaryOperationBase<DEmptyData> {
    protected override double RhsDefaultValue => 1.0;
    protected override DEmptyData GetData(Flow flow, DValue a, DValue b, DValue c) => default;
    protected override double ComputeElement(DEmptyData data, double lhs, double rhs, double aux) => lhs * rhs + aux;
  }
}