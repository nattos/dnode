using System;
using Unity.VisualScripting;

namespace DNode {
  public abstract class DFrameObjectUnit : DFrameUnit {
    [DoNotSerialize][PortLabelHidden] public ValueInput Input;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Input = ValueInput<DFrameArray<DFrameObject>>("Input");

      DFrameArray<DFrameObject> ComputeFromFlow(Flow flow) {
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        return Compute(flow, input.ValueArray ?? Array.Empty<DFrameObject>());
      }
      result = ValueOutput<DFrameArray<DFrameObject>>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  
    protected abstract DFrameArray<DFrameObject> Compute(Flow flow, DFrameObject[] inputs);
  }
}
