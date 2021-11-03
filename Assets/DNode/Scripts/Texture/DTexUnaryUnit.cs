using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexUnaryUnit : DTexUnit {
    [DoNotSerialize][PortLabelHidden] public ValueInput Input;
    [DoNotSerialize] public ValueInput Bypass;
    [Inspectable] public TextureSizeSource SizeSource = TextureSizeSource.Source;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Input = ValueInput<DFrameTexture>(nameof(Input));
      Bypass = ValueInput<bool>(nameof(Bypass), false);

      DFrameTexture ComputeFromFlow(Flow flow) {
        Texture texture = GetTextureInput(flow, Input, Texture2D.blackTexture);
        if (flow.GetValue<bool>(Bypass)) {
          return new DFrameTexture { Texture = texture };
        }
        TextureSizeSource sizeSource = SizeSource;
        RenderTexture output = DScriptMachine.CurrentInstance.RenderTextureCache.Allocate(texture, sizeSource);
        Compute(flow, texture, output);
        BlitToDebugCaptureTexture(output);
        return new DFrameTexture { Texture = output };
      }
      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected abstract void Compute(Flow flow, Texture input, RenderTexture output);
  }
}
