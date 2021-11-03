using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DDelayTexture : DTexUnit {
    [DoNotSerialize]
    public ValueInput Input;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private RenderTexture _cachedTexture;
    private RenderTexture _nextTexture;
    private int _cachedFrame = -1;
  
    public DDelayTexture() {
      SetCaptureTexturePullHandler(() => {
        BlitToDebugCaptureTexture(_nextTexture);
      });
    }
  
    public override void Dispose() {
      base.Dispose();
      RenderTexture cachedTexture = _cachedTexture;
      RenderTexture nextTexture = _nextTexture;
      DScriptMachine.DelayCall(() => {
        UnityUtils.Destroy(cachedTexture);
        UnityUtils.Destroy(nextTexture);
      });
      _cachedTexture = null;
      _nextTexture = null;
    }

    private void AllocateTexture(int width, int height, ref RenderTexture texture) {
      if (texture == null || texture.width != width || texture.height != height) {
        UnityUtils.Destroy(texture);
        texture = new RenderTexture(width, height, depth: 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, mipCount: 0);
        texture.autoGenerateMips = false;
      }
    }

    private void DestroyTexture(ref RenderTexture texture) {
      UnityUtils.Destroy(texture);
      texture = null;
    }

    private void CopyTexture(Texture texture, ref RenderTexture copyTo) {
      if (texture == null) {
        DestroyTexture(ref copyTo);
        return;
      }
      AllocateTexture(texture.width, texture.height, ref copyTo);
      copyTo.filterMode = texture.filterMode;
      copyTo.wrapMode = texture.wrapMode;
      Graphics.Blit(texture, copyTo);
    }

    protected override void Definition() {
      Input = ValueInput<Texture>(nameof(Input), null).AllowsNull();

      DFrameTexture ComputeFromFlow(Flow flow) {
        int currentFrame = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
        if (currentFrame != _cachedFrame) {
          _cachedTexture = _nextTexture;
          _cachedFrame = currentFrame;
          CopyTexture(flow.GetValue<Texture>(Input), ref _nextTexture);
        }
        return _cachedTexture;
      }

      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
