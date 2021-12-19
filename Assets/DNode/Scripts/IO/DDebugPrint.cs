using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DDebugPrint : DIOOutputNode {
    [DoNotSerialize][PortLabelHidden] public ValueInput Input;

    protected override void Definition() {
      Input = ValueInput<object>("Input", default);
    }

    public override void ComputeFromFlow(Flow flow) {
      if (!Input.hasAnyConnection) {
        return;
      }
      object input = flow.GetValue<object>(Input);
      if (input is DEvent devent) {
        if (devent.IsTriggered) {
          Debug.Log(devent.Value);
        }
      } else if (input is UnityEngine.Object unityObject) {
        Debug.Log(unityObject.name);
      } else {
        Debug.Log(input);
      }
    }
  }
}
