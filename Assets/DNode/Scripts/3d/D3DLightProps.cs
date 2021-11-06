using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DNode {
  public class D3DLightProps : DFrameObjectWithDataUnit<D3DLightProps.Data> {
    public struct Data {
      public HDLightType? Type;
      public DValue? ShapeRadius;
      public DValue? FilterColor;
      public DValue? TemperatureKelvin;
      public DValue? IntensityKilolumens;
      public DValue? Range;
      public DValue? VolumetricsEnabled;
      public DValue? VolumetricsMultiplier;
      public DValue? VolumetricsShadowDimmer;
      public DValue? VolumetricsFadeDistance;
    }

    [DoNotSerialize] public ValueInput Type;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange(0.25)][LogScale] public ValueInput ShapeRadius;
    [DoNotSerialize][Color] public ValueInput FilterColor;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(1500, 20000, 6500)][Label("Temperature (K)")] public ValueInput TemperatureKelvin;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 10000, 1500)][Label("Intensity (Kilolumens)")][LogScale] public ValueInput IntensityKilolumens;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0.0, D3DConstants.DefaultFarWorldRange, D3DConstants.DefaultFarWorldRange)][LogScale] public ValueInput Range;
    [DoNotSerialize][PortLabelHidden][Boolean] public ValueInput VolumetricsEnabled;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0.0, 16.0, 1.0)][Label("Vol Multiplier")] public ValueInput VolumetricsMultiplier;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange(1.0)][Label("Vol Shadow Dimmer")] public ValueInput VolumetricsShadowDimmer;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0.0, D3DConstants.DefaultFarWorldRange, D3DConstants.DefaultFarWorldRange)][LogScale][Label("Vol Fade Distance")] public ValueInput VolumetricsFadeDistance;

    protected override void Definition() {
      base.Definition();
      Type = ValueInput<HDLightType>(nameof(Type), HDLightType.Point);
      ShapeRadius = ValueInput<DEvent>(nameof(ShapeRadius), DEvent.CreateImmediate(0.25, triggered: false));
      FilterColor = ValueInput<DEvent>(nameof(FilterColor), DEvent.CreateImmediate(Color.white, triggered: false));
      TemperatureKelvin = ValueInput<DEvent>(nameof(TemperatureKelvin), DEvent.CreateImmediate(6500, triggered: false));
      IntensityKilolumens = ValueInput<DEvent>(nameof(IntensityKilolumens), DEvent.CreateImmediate(1500, triggered: false));
      Range = ValueInput<DEvent>(nameof(Range), DEvent.CreateImmediate(D3DConstants.DefaultFarWorldRange, triggered: false));
      VolumetricsEnabled = ValueInput<DEvent>(nameof(VolumetricsEnabled), DEvent.CreateImmediate(1.0, triggered: false));
      VolumetricsMultiplier = ValueInput<DEvent>(nameof(VolumetricsMultiplier), DEvent.CreateImmediate(1.0, triggered: false));
      VolumetricsShadowDimmer = ValueInput<DEvent>(nameof(VolumetricsShadowDimmer), DEvent.CreateImmediate(1.0, triggered: false));
      VolumetricsFadeDistance = ValueInput<DEvent>(nameof(VolumetricsFadeDistance), DEvent.CreateImmediate(D3DConstants.DefaultFarWorldRange, triggered: false));
    }

    protected override Data GetData(Flow flow, DFrameObject[] inputs) {
      return new Data {
        Type = flow.GetValue<HDLightType>(Type),
        ShapeRadius = GetNullableDValueFromDEventInput(flow, ShapeRadius),
        FilterColor = GetNullableDValueFromDEventInput(flow, FilterColor),
        TemperatureKelvin = GetNullableDValueFromDEventInput(flow, TemperatureKelvin),
        IntensityKilolumens = GetNullableDValueFromDEventInput(flow, IntensityKilolumens),
        Range = GetNullableDValueFromDEventInput(flow, Range),
        VolumetricsEnabled = GetNullableDValueFromDEventInput(flow, VolumetricsEnabled),
        VolumetricsMultiplier = GetNullableDValueFromDEventInput(flow, VolumetricsMultiplier),
        VolumetricsShadowDimmer = GetNullableDValueFromDEventInput(flow, VolumetricsShadowDimmer),
        VolumetricsFadeDistance = GetNullableDValueFromDEventInput(flow, VolumetricsFadeDistance),
      };
    }

    protected override void ApplyToObject(Data data, int row, GameObject input) {
      var light = LightComponent.GetOrAdd(input);
      if (!light) {
        return;
      }
      if (data.Type != null) {
        light.Type.Value = data.Type.Value;
      }
      if (data.ShapeRadius != null) {
        light.ShapeRadius.Value = (float)data.ShapeRadius.Value[row, 0];
      }
      if (data.FilterColor != null) {
        light.FilterColor.Value = data.FilterColor.Value.ColorFromRow(row);
      }
      if (data.TemperatureKelvin != null) {
        light.TemperatureKelvin.Value = (float)data.TemperatureKelvin.Value[row, 0];
      }
      if (data.IntensityKilolumens != null) {
        light.IntensityLumens.Value = (float)data.IntensityKilolumens.Value[row, 0] * 1000;
      }
      if (data.Range != null) {
        light.Range.Value = (float)data.Range.Value[row, 0];
      }
      if (data.VolumetricsEnabled != null) {
        light.VolumetricsEnabled.Value = data.VolumetricsEnabled.Value[row, 0] > 0.0;
      }
      if (data.VolumetricsMultiplier != null) {
        light.VolumetricsMultiplier.Value = (float)data.VolumetricsMultiplier.Value[row, 0];
      }
      if (data.VolumetricsShadowDimmer != null) {
        light.VolumetricsShadowDimmer.Value = (float)data.VolumetricsShadowDimmer.Value[row, 0];
      }
      if (data.VolumetricsFadeDistance != null) {
        light.VolumetricsFadeDistance.Value = (float)data.VolumetricsFadeDistance.Value[row, 0];
      }
    }
  }
}
