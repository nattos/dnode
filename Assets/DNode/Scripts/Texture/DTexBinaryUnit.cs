using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexBinaryUnit : DTexUnit {
    [DoNotSerialize] public ValueInput A;
    [DoNotSerialize] public ValueInput B;
    [DoNotSerialize] public ValueInput Bypass;

    [Inspectable] public TextureSizeSource SizeSource = TextureSizeSource.Source;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = ValueInput<DFrameTexture>("A");
      B = ValueInput<DFrameTexture>("B");
      Bypass = ValueInput<bool>("Bypass", false);

      DFrameTexture ComputeFromFlow(Flow flow) {
        Texture textureA = DTexUnaryUnit.GetTextureInput(flow, A, Texture2D.blackTexture);
        Texture textureB = DTexUnaryUnit.GetTextureInput(flow, B, Texture2D.whiteTexture);
        if (flow.GetValue<bool>(Bypass)) {
          return new DFrameTexture { Texture = textureA };
        }
        TextureSizeSource sizeSource = SizeSource;
        RenderTexture output = DScriptMachine.CurrentInstance.RenderTextureCache.Allocate(textureA, sizeSource);
        Compute(flow, textureA, textureB, output);
        BlitToDebugCaptureTexture(output);
        return new DFrameTexture { Texture = output };
      }
      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected abstract void Compute(Flow flow, Texture lhs, Texture rhs, RenderTexture output);
  }
}
