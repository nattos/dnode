using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DLatchTexture : DTexUnit {
    [DoNotSerialize] public ValueInput Initial;
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize] public ValueInput Trigger;
    [DoNotSerialize] public ValueInput Reset;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    [DoNotSerialize] public ValueOutput resultPrev;

    private bool _hasLatchedValue;
    private int _currentFrameNumber = 0;
    private RenderTexture _latchedTexture;

    public DLatchTexture() {
      SetCaptureTexturePullHandler(() => {
        BlitToDebugCaptureTexture(_latchedTexture);
      });
    }

    public override void Dispose() {
      base.Dispose();
      RenderTexture latchedTexture = _latchedTexture;
      DScriptMachine.DelayCall(() => {
        UnityUtils.Destroy(latchedTexture);
      });
      _latchedTexture = null;
    }

    private void AllocateTexture(int width, int height) {
      if (_latchedTexture == null || _latchedTexture.width != width || _latchedTexture.height != height) {
        UnityUtils.Destroy(_latchedTexture);
        _latchedTexture = new RenderTexture(width, height, depth: 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, mipCount: 0);
        _latchedTexture.autoGenerateMips = false;
      }
    }

    private void DestroyTexture() {
      UnityUtils.Destroy(_latchedTexture);
      _latchedTexture = null;
    }

    protected override void Definition() {
      Initial = ValueInput<Texture>(nameof(Initial), null).AllowsNull();
      Input = ValueInput<Texture>(nameof(Input), null).AllowsNull();
      Trigger = ValueInput<bool>(nameof(Trigger), false);
      Reset = ValueInput<bool>(nameof(Reset), false);

      DFrameTexture ComputeFromFlow(Flow flow) {
        if (!_hasLatchedValue || flow.GetValue<bool>(Reset)) {
          CopyTexture(flow.GetValue<Texture>(Initial));
          _hasLatchedValue = true;
          return _latchedTexture;
        }
        int frameNumber = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
        if (_currentFrameNumber != frameNumber) {
          _currentFrameNumber = frameNumber;
          bool trigger = flow.GetValue<bool>(Trigger);
          if (trigger) {
            CopyTexture(flow.GetValue<Texture>(Input));
          }
        }
        return _latchedTexture;
      }
      result = ValueOutput<object>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
      resultPrev = ValueOutput<object>("Prev", flow => _latchedTexture);
    }

    private void CopyTexture(Texture texture) {
      if (texture == null) {
        DestroyTexture();
        return;
      }
      AllocateTexture(texture.width, texture.height);
      _latchedTexture.filterMode = texture.filterMode;
      _latchedTexture.wrapMode = texture.wrapMode;
      Graphics.Blit(texture, _latchedTexture);
    }
  }
}
