using System;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DIOFrameOutput : DIOOutputNode {
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize] public ValueInput Bypass;
    [DoNotSerialize] public ValueInput SizeSource;
    [DoNotSerialize] public ValueInput Address;
    [DoNotSerialize] public ValueInput UseAlphaChannel;

    private static Klak.Spout.SpoutResources _spoutResources;
    private RenderTexture _outputTexture;
    private Klak.Spout.SpoutSender _sender;

    public override void AfterAdd() {
      base.AfterAdd();
    }

    public override void Dispose() {
      base.Dispose();
      StopSender();
    }

    private void StartSender() {
      if (_sender) {
        return;
      }
      if (!_spoutResources) {
        _spoutResources = ScriptableObject.CreateInstance<Klak.Spout.SpoutResources>();
        _spoutResources.blitShader = Shader.Find("Hidden/Klak/Spout/Blit");
      }
      var gameObject = new GameObject(nameof(DIOFrameInput), typeof(Klak.Spout.SpoutSender));
      _sender = gameObject.GetComponent<Klak.Spout.SpoutSender>();
      _sender.SetResources(_spoutResources);
      _sender.captureMethod = Klak.Spout.CaptureMethod.Texture;
    }

    private void StopSender() {
      if (!_sender) {
        return;
      }
      var sender = _sender;
      var outputTexture = _outputTexture;
      DScriptMachine.DelayCall(() => {
        UnityUtils.Destroy(sender.gameObject);
        UnityUtils.Destroy(outputTexture);
      });
      _sender = null;
      _outputTexture = null;
    }

    protected override void Definition() {
      Input = ValueInput<DFrameTexture>("Input", default);
      Bypass = ValueInput<bool>("Bypass", false);
      SizeSource = ValueInput<TextureSizeSource>("Size", TextureSizeSource.Source);
      Address = ValueInput<string>("Address", "DNode Output");
      UseAlphaChannel = ValueInput<bool>("AlphaChannel", true);
    }

    public override void ComputeFromFlow(Flow flow) {
      Texture input = DTexUnit.GetTextureInput(flow, Input);
      if (input == null) {
        StopSender();
        return;
      }

      StartSender();
      _sender.spoutName = flow.GetValue<string>(Address);
      _sender.keepAlpha = flow.GetValue<bool>(UseAlphaChannel);

      if (flow.GetValue<bool>(Bypass)) {
        return;
      }

      TextureSizeSource sizeSource = flow.GetValue<TextureSizeSource>(SizeSource);
      Vector2Int size = RenderTextureCache.GetSizeFromSource(input, sizeSource);
      if (_outputTexture == null || _outputTexture.width != size.x || _outputTexture.height != size.y) {
        UnityUtils.Destroy(_outputTexture);
        _outputTexture = new RenderTexture(size.x, size.y, depth: 0, input.graphicsFormat, mipCount: 0);
        _outputTexture.autoGenerateMips = false;
        _sender.sourceTexture = _outputTexture;
      }
      Graphics.Blit(input, _outputTexture);
    }
  }
}
