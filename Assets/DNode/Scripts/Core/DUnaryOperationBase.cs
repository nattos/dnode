using Unity.VisualScripting;

namespace DNode {
  public abstract class DUnaryOperationBase : Unit {
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueInput A;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = ValueInput<DValue>("A", 0);

      DValue ComputeFromFlow(Flow flow) {
        return Compute(flow.GetValue<DValue>(A));
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected abstract DValue Compute(DValue lhs);
  }
}
