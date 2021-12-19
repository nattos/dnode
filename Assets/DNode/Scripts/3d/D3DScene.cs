using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class D3DScene : MultiInputUnit<DFrameNodes> {
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      base.Definition();

      DFrameNodes ComputeFromFlow(Flow flow) {
        List<DFrameNode> outputs = new List<DFrameNode>();
        foreach (var input in multiInputs) {
          if (!input.hasAnyConnection) {
            continue;
          }
          var frameObjects = flow.GetValue<DFrameNodes>(input);
          if (frameObjects.Nodes != null) {
            outputs.AddRange(frameObjects.Nodes);
          }
        }
        DMutableFrameArray<DFrameNode> result = new DMutableFrameArray<DFrameNode>(outputs);
        return result.ToValue();
      }

      result = ValueOutput<DFrameNodes>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
