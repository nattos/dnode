using Unity.VisualScripting;

namespace DNode {
  public class DRowCount : Unit {
    [DoNotSerialize][PortLabelHidden][NoEditor] public ValueInput A;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = ValueInput<DValue>(nameof(A), 0);

      int ComputeFromFlow(Flow flow) {
        return flow.GetValue<DValue>(A).Rows;
      }
      result = ValueOutput<int>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
