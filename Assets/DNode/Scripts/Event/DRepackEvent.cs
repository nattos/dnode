using System;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DRepackEvent : Unit {
    [DoNotSerialize][PortLabelHidden][Vector4][ShortEditor] public ValueInput Values;
    [DoNotSerialize] public ValueInput Triggered;

    [DoNotSerialize]
    public ValueOutput resultEvent;
  
    [DoNotSerialize]
    public ValueOutput resultValue;
  
    [DoNotSerialize]
    public ValueOutput resultTriggered;

    protected override void Definition() {
      Values = ValueInput<DValue>("Values", default);
      Triggered = ValueInput<bool>("Trigger", false);

      DEvent ComputeFromFlow(Flow flow) {
        DValue values = flow.GetValue<DValue>(Values);
        bool triggered = flow.GetValue<bool>(Triggered);
        return DEvent.CreateImmediate(values, triggered);
      }
      var resultFunc = DNodeUtils.CachePerFrame(ComputeFromFlow);
      resultEvent = ValueOutput<DEvent>("Event", resultFunc);
      resultValue = ValueOutput<DValue>("Value", flow => resultFunc(flow).Value);
      resultTriggered = ValueOutput<bool>("Trigger", flow => resultFunc(flow).IsTriggered);
    }
  }
}
