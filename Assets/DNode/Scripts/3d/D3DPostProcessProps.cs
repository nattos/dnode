using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DPostProcessProps : DFrameUnit {
    [DoNotSerialize][PortLabelHidden][Scalar][ExposureRange] public ValueInput  ExposureCompensation;

    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 4, 1)] public ValueInput MotionBlurIntensity;

    [DoNotSerialize] public ValueInput FilmGrainType;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.2)] public ValueInput FilmGrainIntensity;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.8)] public ValueInput FilmGrainResponse;

    [DoNotSerialize][PortLabelHidden][Scalar][Range(-4, 4, 0)] public ValueInput BloomThreshold;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.2)] public ValueInput BloomIntensity;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.65)] public ValueInput BloomScatter;
    [DoNotSerialize][Color] public ValueInput BloomTint;

    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 4, 0.4)] public ValueInput AmbientOcclusionIntensity;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 5, 0.5)] public ValueInput AmbientOcclusionRadius;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      ExposureCompensation = ValueInput<DEvent>(nameof(ExposureCompensation), DEvent.CreateImmediate(0.0, triggered: false));
      MotionBlurIntensity = ValueInput<DEvent>(nameof(MotionBlurIntensity), DEvent.CreateImmediate(1.0, triggered: false));
      FilmGrainType = ValueInput<UnityEngine.Rendering.HighDefinition.FilmGrainLookup>(nameof(FilmGrainType), UnityEngine.Rendering.HighDefinition.FilmGrainLookup.Thin1);
      FilmGrainIntensity = ValueInput<DEvent>(nameof(FilmGrainIntensity), DEvent.CreateImmediate(0.2, triggered: false));
      FilmGrainResponse = ValueInput<DEvent>(nameof(FilmGrainResponse), DEvent.CreateImmediate(0.8, triggered: false));
      BloomThreshold = ValueInput<DEvent>(nameof(BloomThreshold), DEvent.CreateImmediate(0.0, triggered: false));
      BloomIntensity = ValueInput<DEvent>(nameof(BloomIntensity), DEvent.CreateImmediate(0.2, triggered: false));
      BloomScatter = ValueInput<DEvent>(nameof(BloomScatter), DEvent.CreateImmediate(0.65, triggered: false));
      BloomTint = ValueInput<DEvent>(nameof(BloomTint), DEvent.CreateImmediate(Color.white, triggered: false));
      AmbientOcclusionIntensity = ValueInput<DEvent>(nameof(AmbientOcclusionIntensity), DEvent.CreateImmediate(0.4, triggered: false));
      AmbientOcclusionRadius = ValueInput<DEvent>(nameof(AmbientOcclusionRadius), DEvent.CreateImmediate(0.5, triggered: false));
 
      DFrameCommand ComputeFromFlow(Flow flow) {
        var env = DScriptMachine.CurrentInstance.EnvironmentComponent;
        env.ExposureCompensation.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, ExposureCompensation)?.FloatFromRow(0));
        env.MotionBlurIntensity.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, MotionBlurIntensity)?.FloatFromRow(0));
        // env.FilmGrainType.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, FilmGrainType)?.FloatFromRow(0));
        env.FilmGrainIntensity.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, FilmGrainIntensity)?.FloatFromRow(0));
        env.FilmGrainResponse.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, FilmGrainResponse)?.FloatFromRow(0));
        env.BloomThreshold.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, BloomThreshold)?.FloatFromRow(0));
        env.BloomIntensity.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, BloomIntensity)?.FloatFromRow(0));
        env.BloomScatter.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, BloomScatter)?.FloatFromRow(0));
        env.BloomTint.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, BloomTint)?.ColorFromRow(0));
        env.AmbientOcclusionIntensity.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, AmbientOcclusionIntensity)?.FloatFromRow(0));
        env.AmbientOcclusionRadius.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, AmbientOcclusionRadius)?.FloatFromRow(0));
        return DFrameCommand.Empty;
      }

      result = ValueOutput<DFrameCommand>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
