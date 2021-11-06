using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DSky : DFrameUnit {
    [DoNotSerialize][PortLabelHidden][Scalar][ExposureRange] public ValueInput Exposure;
    [DoNotSerialize][Color] public ValueInput TopColor;
    [DoNotSerialize][Color] public ValueInput MiddleColor;
    [DoNotSerialize][Color] public ValueInput BottomColor;
    [DoNotSerialize][Texture][Label("BG")] public ValueInput BackgroundTexture;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][Label("BG Alpha")] public ValueInput BackgroundTextureAlpha;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Exposure = ValueInput<DEvent>(nameof(Exposure), 0.0f);
      TopColor = ValueInput<DEvent>(nameof(TopColor), DEvent.CreateImmediate(Color.black, triggered: false));
      MiddleColor = ValueInput<DEvent>(nameof(MiddleColor), DEvent.CreateImmediate(Color.black, triggered: false));
      BottomColor = ValueInput<DEvent>(nameof(BottomColor), DEvent.CreateImmediate(Color.black, triggered: false));
      BackgroundTexture = ValueInput<Texture>(nameof(BackgroundTexture), null).AllowsNull();
      BackgroundTextureAlpha = ValueInput<DEvent>(nameof(BackgroundTextureAlpha), 0.0f);
 
      DFrameCommand ComputeFromFlow(Flow flow) {
        var env = DScriptMachine.CurrentInstance.EnvironmentComponent;
        env.SkyGradientExposure.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, Exposure)?.FloatFromRow(0) + 15.0f);
        env.SkyGradientTopColor.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, TopColor)?.ColorFromRow(0));
        env.SkyGradientMiddleColor.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, MiddleColor)?.ColorFromRow(0));
        env.SkyGradientBottomColor.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, BottomColor)?.ColorFromRow(0));
        env.BackgroundTexture.MaybeSetValue(flow.GetValue<Texture>(BackgroundTexture));
        env.BackgroundTextureAlpha.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, BackgroundTextureAlpha)?.FloatFromRow(0));
        return DFrameCommand.Empty;
      }

      result = ValueOutput<DFrameCommand>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
