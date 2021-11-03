using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexBlur : DTexUnaryPreserveAlphaBlitUnit {
    private const float _epsilon = 1e-3f;
    private const float _minStepSize = 8.0f;
    private const float _maxStepSize = 128.0f;

    private int _Strength = Shader.PropertyToID("_Strength");
    private int _TargetStepSize = Shader.PropertyToID("_TargetStepSize");
    private int _Axis = Shader.PropertyToID("_Axis");
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Strength;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Quality;
    protected override void Definition() {
      base.Definition();
      Strength = ValueInput<DValue>("Strength", 0.2);
      Quality = ValueInput<DValue>("Quality", 0.5);
    }
    protected override string ShaderPath => "Hidden/TexBlur";
    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetFloat(_Strength, Mathf.Max(0.0f, flow.GetValue<DValue>(Strength)));
      float stepSizeT = Mathf.Clamp01(flow.GetValue<DValue>(Quality));
      stepSizeT = 1 - stepSizeT;
      stepSizeT *= stepSizeT;
      stepSizeT = 1 - stepSizeT;
      material.SetFloat(_TargetStepSize, _minStepSize * stepSizeT + _maxStepSize * (1.0f - stepSizeT));
    }
    protected override void Compute(Flow flow, Texture input, RenderTexture output) {
      Material material = CreateMaterial();
      SetMaterialProperties(flow, material);
      if (material.GetFloat(_Strength) < _epsilon) {
        Graphics.Blit(input, output);
        return;
      }
      RenderTexture tmp = DScriptMachine.CurrentInstance.RenderTextureCache.Allocate(output.width, output.height);
      material.SetFloat(_Axis, 0.0f);
      Graphics.Blit(input, tmp, material);
      material.SetFloat(_Axis, 1.0f);
      Graphics.Blit(tmp, output, material);
      DScriptMachine.CurrentInstance.RenderTextureCache.Free(tmp);
    }
  }
}
