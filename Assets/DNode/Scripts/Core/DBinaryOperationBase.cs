using Unity.VisualScripting;

namespace DNode {
  public abstract class DBinaryOperationBase : Unit {
    [DoNotSerialize][PortLabelHidden] public ValueInput A;
    [DoNotSerialize][PortLabelHidden] public ValueInput B;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = LhsPort = ValueInput<DValue>(LhsName, LhsDefaultValue);
      B = RhsPort = ValueInput<DValue>(RhsName, RhsDefaultValue);

      DValue ComputeFromFlow(Flow flow) {
        return Compute(flow.GetValue<DValue>(A), flow.GetValue<DValue>(B));
      }

      result = ValueOutput<DValue>(ResultName, DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected virtual string LhsName => "A";
    protected virtual string RhsName => "B";
    protected virtual ValueInput LhsPort { set { A = value; } }
    protected virtual ValueInput RhsPort { set { B = value; } }
    protected virtual double LhsDefaultValue => 0.0;
    protected virtual double RhsDefaultValue => 0.0;
    protected virtual string ResultName => "result";

    protected abstract DValue Compute(DValue lhs, DValue rhs);
  }
}
