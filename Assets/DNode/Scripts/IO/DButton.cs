using System;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DButton : Unit {
    [DoNotSerialize][PortLabelHidden][Boolean][ShortEditor] public ValueInput Trigger;
    [DoNotSerialize] public ValueOutput result;

    protected override void Definition() {
      Trigger = ValueInput<DValue>(nameof(Trigger), 0.0);

      bool ComputeFromFlow(Flow flow) {
        bool triggered = flow.GetValue<DValue>(Trigger).BoolFromRow(0);
        if (triggered) {
          Trigger.SetDefaultValue((DValue)0.0);
        }
        return triggered;
      }
      result = ValueOutput<bool>("Event", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
