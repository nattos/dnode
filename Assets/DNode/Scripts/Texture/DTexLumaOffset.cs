using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexLumaOffset : DTexBinaryBlitUnit {
    private int _ColorBasis = Shader.PropertyToID("_ColorBasis");
    private int _BaseValue = Shader.PropertyToID("_BaseValue");
    private int _Shift = Shader.PropertyToID("_Shift");
    private int _ColorBasis2 = Shader.PropertyToID("_ColorBasis2");
    private int _BaseValue2 = Shader.PropertyToID("_BaseValue2");
    private int _Shift2 = Shader.PropertyToID("_Shift2");

    [DoNotSerialize][Color] public ValueInput ColorBasis;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput BaseValue;
    [DoNotSerialize][PortLabelHidden][Vector2][OneRange] public ValueInput Shift;
    [DoNotSerialize][Color] public ValueInput ColorBasis2;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput BaseValue2;
    [DoNotSerialize][PortLabelHidden][Vector2][OneRange] public ValueInput Shift2;
    [DoNotSerialize] public ValueInput Wrap;

    private bool _useSecondBasis = false;
    [Serialize][Inspectable] public bool UseSecondBasis {
      get => _useSecondBasis;
      set {
        _useSecondBasis = value;
        PortsChanged();
      }
    }

    protected override void Definition() {
      base.Definition();

      ColorBasis = ValueInput<DValue>(nameof(ColorBasis), Color.white);
      BaseValue = ValueInput<DValue>(nameof(BaseValue), 0.5f);
      Shift = ValueInput<DValue>(nameof(Shift), Vector2.zero);
      if (_useSecondBasis) {
        ColorBasis2 = ValueInput<DValue>(nameof(ColorBasis2), Color.white);
        BaseValue2 = ValueInput<DValue>(nameof(BaseValue2), 0.5f);
        Shift2 = ValueInput<DValue>(nameof(Shift2), Vector2.zero);
      }
      Wrap = ValueInput<DTexWrapMode>(nameof(Wrap), DTexWrapMode.Clamp);
    }

    protected override string ShaderPath => "Hidden/TexLumaOffset";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      Color colorBasis = flow.GetValue<DValue>(ColorBasis);
      float length = colorBasis.r + colorBasis.g + colorBasis.b;
      if (length < 0.00000001f) {
        colorBasis.r = 1 / 3.0f;
        colorBasis.g = 1 / 3.0f;
        colorBasis.b = 1 / 3.0f;
      } else {
        colorBasis.r /= length;
        colorBasis.g /= length;
        colorBasis.b /= length;
      }
      material.SetColor(_ColorBasis, colorBasis);
      material.SetFloat(_BaseValue, flow.GetValue<DValue>(BaseValue));
      material.SetVector(_Shift, (Vector2)flow.GetValue<DValue>(Shift));
      Color colorBasis2 = flow.GetValue<DValue>(ColorBasis2);
      float length2 = colorBasis2.r + colorBasis2.g + colorBasis2.b;
      if (length2 < 0.00000001f) {
        colorBasis2.r = 1 / 3.0f;
        colorBasis2.g = 1 / 3.0f;
        colorBasis2.b = 1 / 3.0f;
      } else {
        colorBasis2.r /= length2;
        colorBasis2.g /= length2;
        colorBasis2.b /= length2;
      }
      material.SetColor(_ColorBasis2, colorBasis2);
      material.SetFloat(_BaseValue2, flow.GetValue<DValue>(BaseValue2));
      material.SetVector(_Shift2, (Vector2)flow.GetValue<DValue>(Shift2));
    }

    protected override void Blit(Flow flow, Texture lhs, Texture rhs, RenderTexture output, Material material) {
      var wrapMode = flow.GetValue<DTexWrapMode>(Wrap);
      lhs.wrapMode = wrapMode == DTexWrapMode.Wrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
      base.Blit(flow, lhs, rhs, output, material);
    }
  }
}
