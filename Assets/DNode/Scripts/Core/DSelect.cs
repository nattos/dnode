using System;
using Unity.VisualScripting;

namespace DNode {
  public class DSelect : DMultiInputUnit<DValue> {
    [DoNotSerialize]
    public ValueInput Case;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Case = ValueInput<int>("Case", 0);

      base.Definition();

      DValue ComputeFromFlow(Flow flow) {
        int caseValue = flow.GetValue<int>(Case);
        int inputCount = multiInputs.Count;
        int actualCaseValue = Math.Max(0, Math.Min(inputCount - 1, caseValue));
        return flow.GetValue<DValue>(multiInputs[actualCaseValue]);
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
