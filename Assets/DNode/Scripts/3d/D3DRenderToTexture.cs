using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class D3DRenderToTexture : DFrameUnit {
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
        return DScriptMachine.CurrentInstance.RenderNodesToTexture(flow.GetValue<DFrameNodes>(Inputs), SizeSource);
      }
      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
