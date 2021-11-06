using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexGrayscale : DTexUnaryBlitUnit {
    private int _ColorBasis = Shader.PropertyToID("_ColorBasis");

    [DoNotSerialize][Color] public ValueInput ColorBasis;

    protected override void Definition() {
      base.Definition();

      ColorBasis = ValueInput<DValue>(nameof(ColorBasis), Color.white);
    }

    protected override string ShaderPath => "Hidden/TexGrayscale";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      Color colorBasis = flow.GetValue<DValue>(ColorBasis);
      float length = colorBasis.r + colorBasis.g + colorBasis.b;
      if (length < UnityUtils.DefaultEpsilon) {
        colorBasis.r = 1 / 3.0f;
        colorBasis.g = 1 / 3.0f;
        colorBasis.b = 1 / 3.0f;
      } else {
        colorBasis.r /= length;
        colorBasis.g /= length;
        colorBasis.b /= length;
      }
      material.SetColor(_ColorBasis, colorBasis);
    }
  }
}
