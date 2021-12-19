using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DAnyTrigger : MultiInputUnit<bool> {
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      base.Definition();

      bool ComputeFromFlow(Flow flow) {
        bool triggered = false;
        foreach (var input in multiInputs) {
          if (!input.hasAnyConnection) {
            continue;
          }
          if (flow.GetValue<bool>(input)) {
            triggered = true;
          }
        }
        return triggered;
      }
      result = ValueOutput<bool>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
