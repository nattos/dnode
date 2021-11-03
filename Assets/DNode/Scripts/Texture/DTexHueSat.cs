using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexHueSat : DTexUnaryBlitUnit {
    private int _HSL = Shader.PropertyToID("_HSL");
    private int _Colorize = Shader.PropertyToID("_Colorize");

    [DoNotSerialize][PortLabelHidden][Scalar][RotationRange][ClampMode(ClampMode.Wrap)][ShortEditor] public ValueInput HueShift;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 1)][ShortEditor] public ValueInput Saturation;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 1)][ShortEditor] public ValueInput Lightness;
    [DoNotSerialize] public ValueInput Colorize;

    protected override void Definition() {
      base.Definition();

      HueShift = ValueInput<DValue>(nameof(HueShift), 0.0);
      Saturation = ValueInput<DValue>(nameof(Saturation), 1.0);
      Lightness = ValueInput<DValue>(nameof(Lightness), 1.0);
      Colorize = ValueInput<bool>(nameof(Colorize), false);
    }

    protected override string ShaderPath => "Hidden/TexHueSat";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetVector(_HSL, new Vector3(flow.GetValue<DValue>(HueShift) / 360.0f * Mathf.PI * 2, flow.GetValue<DValue>(Saturation), flow.GetValue<DValue>(Lightness)));
      material.SetInt(_Colorize, flow.GetValue<bool>(Colorize) ? 1 : 0);
    }
  }
}
