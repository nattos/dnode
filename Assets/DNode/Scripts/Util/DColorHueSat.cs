using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DColorHueSat : DArrayOperationBase<DColorHueSat.Data> {
    public struct Data {
      public DValue HueShift;
      public DValue Saturation;
      public DValue Lightness;
    }

    [DoNotSerialize][PortLabelHidden][Scalar][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput HueShift;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 1)] public ValueInput Saturation;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(-16, 16, 0)] public ValueInput Lightness;

    protected override void Definition() {
      base.Definition();
      HueShift = ValueInput<DValue>("HueShift", 0.0);
      Saturation = ValueInput<DValue>("Saturation", 1.0);
      Lightness = ValueInput<DValue>("Lightness", 0.0);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = new Data {
        HueShift = flow.GetValue<DValue>(HueShift),
        Saturation = flow.GetValue<DValue>(Saturation),
        Lightness = flow.GetValue<DValue>(Lightness),
      };
      int rows = Math.Max(input.Rows, Math.Max(data.HueShift.Rows, Math.Max(data.Saturation.Rows, data.Lightness.Rows)));
      return (rows, 4);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      int columns = result.Columns;
      for (int i = 0; i < result.Rows; ++i) {
        float hueShift = (float)data.HueShift[i, 0];
        float saturation = (float)data.Saturation[i, 0];
        float lightness = (float)data.Lightness[i, 0];
        Color inputColor = input.ColorFromRow(i, Color.black);
        Vector4 inputHsl = UnityUtils.ToHsl(inputColor);
        Vector4 outputHsl = inputHsl;
        outputHsl.x += hueShift;
        outputHsl.y *= saturation;
        outputHsl.z += lightness;
        Color outputColor = UnityUtils.FromHsl(outputHsl);
        result[i, 0] = outputColor.r;
        result[i, 1] = outputColor.g;
        result[i, 2] = outputColor.b;
        result[i, 3] = outputColor.a;
      }
    }
  }
}
