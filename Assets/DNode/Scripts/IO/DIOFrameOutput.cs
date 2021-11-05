using System;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DIOFrameOutput : DIOOutputNode {
    [DoNotSerialize] public ValueInput Source;
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize] public ValueInput Bypass;
    [DoNotSerialize] public ValueInput SizeSource;
    [DoNotSerialize] public ValueInput Address;
    [DoNotSerialize] public ValueInput UseAlphaChannel;

    private RenderTexture _outputTexture;
    private DIOFrameIOTechnique _currentTechnique;
    private IFrameSender _sender;

    public override void AfterAdd() {
      base.AfterAdd();
    }

    public override void Dispose() {
      base.Dispose();
      StopSender();
    }

    private void StartSender(DIOFrameIOTechnique technique) {
      if (_sender?.IsAlive == true && _currentTechnique == technique) {
        return;
      }
      StopSender();
      _currentTechnique = technique;
      _sender = FrameSenders.CreateSender(technique);
      _sender.StartSender();
    }

    private void StopSender() {
      if (_sender?.IsAlive != true) {
        return;
      }
      var sender = _sender;
      var outputTexture = _outputTexture;
      DScriptMachine.DelayCall(() => {
        sender?.Dispose();
        UnityUtils.Destroy(outputTexture);
      });
      _sender = null;
      _outputTexture = null;
    }

    protected override void Definition() {
      Source = ValueInput<DIOFrameIOTechnique>("Source", DIOFrameIOTechnique.DefaultLocal);
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

      StartSender(flow.GetValue<DIOFrameIOTechnique>(Source));
      _sender.Name = flow.GetValue<string>(Address);
      _sender.UseAlphaChannel = flow.GetValue<bool>(UseAlphaChannel);
      _sender.StartSender();

      if (flow.GetValue<bool>(Bypass)) {
        return;
      }

      TextureSizeSource sizeSource = flow.GetValue<TextureSizeSource>(SizeSource);
      Vector2Int size = RenderTextureCache.GetSizeFromSource(input, sizeSource);
      if (_outputTexture == null || _outputTexture.width != size.x || _outputTexture.height != size.y) {
        UnityUtils.Destroy(_outputTexture);
        _outputTexture = new RenderTexture(size.x, size.y, depth: 0, RenderTextureFormat.BGRA32, mipCount: 0);
        _outputTexture.autoGenerateMips = false;
      }
      _sender.TextureToSend = _outputTexture;
      Graphics.Blit(input, _outputTexture);
    }
  }
}
