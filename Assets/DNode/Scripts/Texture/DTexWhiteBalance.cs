using Unity.VisualScripting;
using UnityEngine;
using Unity​Engine.Rendering;

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
      material.SetVector(_ColorBalance, GetColorBalanceCoeffs(tempurature, tint));
    }
    static Vector3 GetColorBalanceCoeffs(float temperature, float tint) {
        // Range ~[-1.5;1.5] works best
        float t1 = temperature / 65f;
        float t2 = tint / 65f;

        // Get the CIE xy chromaticity of the reference white point.
        // Note: 0.31271 = x value on the D65 white point
        float x = 0.31271f - t1 * (t1 < 0f ? 0.1f : 0.05f);
        float y = ColorUtils.StandardIlluminantY(x) + t2 * 0.05f;

        // Calculate the coefficients in the LMS space.
        var w1 = new Vector3(0.949237f, 1.03542f, 1.08728f); // D65 white point
        var w2 = ColorUtils.CIExyToLMS(x, y);
        return new Vector3(w1.x / w2.x, w1.y / w2.y, w1.z / w2.z);
    }
  }
}
