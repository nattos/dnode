using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DStandardMaterial : DFrameObjectWithDataUnit<D3DStandardMaterial.Data> {
    public struct Data {
      public DValue? BaseColor;
      public Texture BaseTexture;
      public DValue? Metallic;
      public DValue? Smoothness;
      public DValue? EmissionColor;
      public DValue? EmissionExposureWeight;
      public DValue? EmissionMultiplyWithBase;
    }

    [DoNotSerialize][PortLabelHidden][Color] public ValueInput BaseColor;
    [DoNotSerialize][Texture] public ValueInput BaseTexture;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput Metallic;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput Smoothness;
    [DoNotSerialize][PortLabelHidden][Color] public ValueInput EmissionColor;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange(defaultValue: 1.0)] public ValueInput EmissionExposureWeight;
    [DoNotSerialize][PortLabelHidden][Boolean] public ValueInput EmissionMultiplyWithBase;

    protected override void Definition() {
      base.Definition();
      BaseColor = ValueInput<DEvent>("BaseColor", default);
      BaseTexture = ValueInput<Texture>("BaseTexture", null).AllowsNull();
      Metallic = ValueInput<DEvent>("Metallic", default);
      Smoothness = ValueInput<DEvent>("Smoothness", default);
      EmissionColor = ValueInput<DEvent>("EmissionColor", default);
      EmissionExposureWeight = ValueInput<DEvent>("EmissionExposureWeight", default);
      EmissionMultiplyWithBase = ValueInput<DEvent>("EmissionMultiplyWithBase", default);
    }

    protected override Data GetData(Flow flow, DFrameObject[] inputs) {
      return new Data {
        BaseColor = GetNullableDValueFromDEventInput(flow, BaseColor),
        BaseTexture = GetNullableDFrameTexture(flow, BaseTexture),
        Metallic = GetNullableDValueFromDEventInput(flow, Metallic),
        Smoothness = GetNullableDValueFromDEventInput(flow, Smoothness),
        EmissionColor = GetNullableDValueFromDEventInput(flow, EmissionColor),
        EmissionExposureWeight = GetNullableDValueFromDEventInput(flow, EmissionExposureWeight),
        EmissionMultiplyWithBase = GetNullableDValueFromDEventInput(flow, EmissionMultiplyWithBase),
      };
    }

    protected override void ApplyToObject(Data data, int row, GameObject input) {
      var material = MaterialComponent.GetOrAdd(input);
      if (!material) {
        return;
      }
      if (data.BaseColor != null) {
        material.BaseColor.Value = data.BaseColor.Value.ColorFromRow(row);
      }
      if (data.BaseTexture != null) {
        material.BaseTexture.Value = data.BaseTexture;
      }
      if (data.Metallic != null) {
        material.Metallic.Value = (float)data.Metallic.Value[row, 0];
      }
      if (data.Smoothness != null) {
        material.Smoothness.Value = (float)data.Smoothness.Value[row, 0];
      }
      if (data.EmissionColor != null) {
        material.EmissionColor.Value = data.EmissionColor.Value.ColorFromRow(row);
      }
      if (data.EmissionExposureWeight != null) {
        material.EmissionExposureWeight.Value = (float)data.EmissionExposureWeight.Value[row, 0];
      }
      if (data.EmissionMultiplyWithBase != null) {
        material.EmissionMultiplyWithBase.Value = data.EmissionMultiplyWithBase.Value[row, 0] > 0.0;
      }
    }
  }
}
