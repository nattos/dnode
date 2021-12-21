using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexGenUnit<TData> : DTexUnit {
    [DoNotSerialize] public ValueInput Bypass;

    [Inspectable] public TextureGenSizeSource SizeSource = TextureGenSizeSource.EnvOutput;
    [Inspectable] public int FixedWidth = 1024;
    [Inspectable] public int FixedHeight = 1024;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Bypass = ValueInput<bool>(nameof(Bypass), false);

      DFrameTexture ComputeFromFlow(Flow flow) {
        if (flow.GetValue<bool>(Bypass)) {
          return new DFrameTexture { Texture = GetBypassTexture(flow) };
        }

        TData data = GetData(flow);
        TextureSizeSource sizeSource;
        Vector2Int? sourceSize = GetSourceSize(data);
        switch (SizeSource) {
          default:
          case TextureGenSizeSource.Auto:
            sizeSource = TextureSizeSource.Source;
            break;
          case TextureGenSizeSource.Fixed:
            sizeSource = TextureSizeSource.Source;
            sourceSize = new Vector2Int(Mathf.Max(1, FixedWidth), Mathf.Max(1, FixedHeight));
            break;
          case TextureGenSizeSource.EnvInput:
            sizeSource = TextureSizeSource.EnvInput;
            break;
          case TextureGenSizeSource.EnvOutput:
            sizeSource = TextureSizeSource.EnvOutput;
            break;
        }

        RenderTexture output = DScriptMachine.CurrentInstance.RenderTextureCache.Allocate(RenderTextureCache.GetSizeFromSource(sourceSize, sizeSource));
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
