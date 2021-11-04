using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DGenNoise : DTexGenBlitUnit {
    private int _Phase = Shader.PropertyToID("_Phase");
    private int _Alpha = Shader.PropertyToID("_Alpha");
    private int _Granularity = Shader.PropertyToID("_Granularity");

    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Granularity;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ClampMode(ClampMode.Wrap)][ShortEditor] public ValueInput Phase;
    [DoNotSerialize][PortLabelHidden][Vector4][Range(0.0, 1.0, 0.0, 0.0, 0.0, 1.0)][ShortEditor] public ValueInput Alpha;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange][ShortEditor] public ValueInput Stretch;

    protected override void Definition() {
      base.Definition();

      Granularity = ValueInput<DValue>(nameof(Granularity), 0.0);
      Phase = ValueInput<DValue>(nameof(Phase), Vector4.zero);
      Alpha = ValueInput<DValue>(nameof(Alpha), new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
      Stretch = ValueInput<DValue>(nameof(Stretch), new Vector2(0.0f, 0.0f));
    }

    protected override string ShaderPath => "Hidden/GenNoise";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetVector(_Phase, (Vector4)flow.GetValue<DValue>(Phase));
      material.SetVector(_Alpha, (Vector4)flow.GetValue<DValue>(Alpha));
    }

    protected override void Blit(Flow flow, RenderTexture output, Material material) {
      double granularity = flow.GetValue<DValue>(Granularity);
      Vector2 stretch = flow.GetValue<DValue>(Stretch);
      float pixelSizeX = (float)UnityUtils.Lerp(output.width, output.height / (double)output.width, granularity);
      float pixelSizeY = (float)UnityUtils.Lerp(output.height, 1.0, granularity);
      material.SetVector(_Granularity, new Vector4(pixelSizeX * (1.0f - stretch.x), pixelSizeY * (1.0f - stretch.y), 1.0f / pixelSizeX, 1.0f / pixelSizeY));
      Graphics.Blit(null, output, material);
    }
  }
}
