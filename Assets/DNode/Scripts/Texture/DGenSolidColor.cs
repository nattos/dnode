using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DGenSolidColor : DTexGenBlitUnit {
    private int _Color = Shader.PropertyToID("_Color");
    private int _Gain = Shader.PropertyToID("_Gain");

    [DoNotSerialize][PortLabelHidden][Color][ShortEditor] public ValueInput Color;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(D3DConstants.DefaultMinEv, D3DConstants.DefaultMaxEv, 0)][ShortEditor] public ValueInput Exposure;

    protected override void Definition() {
      base.Definition();

      Color = ValueInput<DValue>(nameof(Color), UnityEngine.Color.white);
      Exposure = ValueInput<DValue>(nameof(Exposure), 0.0f);
    }

    protected override string ShaderPath => "Hidden/GenSolidColor";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetColor(_Color, flow.GetValue<DValue>(Color));
      material.SetFloat(_Gain, Mathf.Pow(2.0f, flow.GetValue<DValue>(Exposure)));
    }
  }
}
