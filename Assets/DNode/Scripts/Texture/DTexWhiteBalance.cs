using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexWhiteBalance : DTexUnaryBlitUnit {
    private int _ColorBalance = Shader.PropertyToID("_ColorBalance");

    [DoNotSerialize][PortLabelHidden][Scalar][Range(-100.0, 100.0, 0.0)][ShortEditor] public ValueInput Temperature;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(-100.0, 100.0, 0.0)][ShortEditor] public ValueInput Tint;

    protected override void Definition() {
      base.Definition();

      Temperature = ValueInput<DValue>(nameof(Temperature), 0.0);
      Tint = ValueInput<DValue>(nameof(Tint), 0.0);
    }

    protected override string ShaderPath => "Hidden/TexWhiteBalance";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      float tempurature = flow.GetValue<DValue>(Temperature);
      float tint = flow.GetValue<DValue>(Tint);
      material.SetVector(_ColorBalance,  UnityEngine.Rendering.HighDefinition.HDRenderPipeline.GetColorBalanceCoeffs(tempurature, tint));
    }
  }
}
