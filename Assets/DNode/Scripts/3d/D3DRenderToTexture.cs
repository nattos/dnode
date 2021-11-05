using System.Linq;
using UnityEngine;
using Unity.VisualScripting;

namespace DNode {
  public class D3DRenderToTexture : DTexUnit {
    [DoNotSerialize][PortLabelHidden] public ValueInput Inputs;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    [Inspectable] public TextureSizeSource SizeSource = TextureSizeSource.EnvOutput;

    protected override void Definition() {
      base.Definition();

      Inputs = ValueInput<DFrameNodes>("Inputs", default);

      DFrameTexture ComputeFromFlow(Flow flow) {
        if (!Inputs.connections.Any()) {
          return UnityUtils.BlankTexture;
        }
        Texture result = DScriptMachine.CurrentInstance.RenderNodesToTexture(flow.GetValue<DFrameNodes>(Inputs), SizeSource);
        BlitToDebugCaptureTexture(result);
        return result;
      }
      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
