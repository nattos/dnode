using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexGenUnit<TData> : DTexUnit {
    [DoNotSerialize] public ValueInput Bypass;

    [Inspectable] public TextureSizeSource SizeSource = TextureSizeSource.EnvOutput;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Bypass = ValueInput<bool>(nameof(Bypass), false);

      DFrameTexture ComputeFromFlow(Flow flow) {
        if (flow.GetValue<bool>(Bypass)) {
          return new DFrameTexture { Texture = GetBypassTexture(flow) };
        }
        TextureSizeSource sizeSource = SizeSource;
        TData data = GetData(flow);
        RenderTexture output = DScriptMachine.CurrentInstance.RenderTextureCache.Allocate(RenderTextureCache.GetSizeFromSource(GetSourceSize(data), sizeSource));
        Compute(flow, data, output);
        BlitToDebugCaptureTexture(output);
        return new DFrameTexture { Texture = output };
      }
      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected virtual Texture GetBypassTexture(Flow flow) => Texture2D.whiteTexture;
    protected virtual Vector2Int? GetSourceSize(TData data) => null;
    protected abstract TData GetData(Flow flow);
    protected abstract void Compute(Flow flow, TData data, RenderTexture output);
  }
}
