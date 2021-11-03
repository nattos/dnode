using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexLevels : DTexUnaryPreserveAlphaBlitUnit {
    private int _Blacks = Shader.PropertyToID("_Blacks");
    private int _Shadows = Shader.PropertyToID("_Shadows");
    private int _Mids = Shader.PropertyToID("_Mids");
    private int _Highlights = Shader.PropertyToID("_Highlights");
    private int _Whites = Shader.PropertyToID("_Whites");

    [DoNotSerialize] public ValueInput Blacks;
    [DoNotSerialize] public ValueInput Shadows;
    [DoNotSerialize] public ValueInput Mids;
    [DoNotSerialize] public ValueInput Highlights;
    [DoNotSerialize] public ValueInput Whites;

    protected override void Definition() {
      base.Definition();

      Blacks = ValueInput<float>("Blacks", 0.0f);
      Shadows = ValueInput<float>("Shadows", 0.0f);
      Mids = ValueInput<float>("Mids", 0.0f);
      Highlights = ValueInput<float>("Highlights", 0.0f);
      Whites = ValueInput<float>("Whites", 0.0f);
    }

    protected override string ShaderPath => "Hidden/TexLevels";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetFloat(_Blacks, flow.GetValue<float>(Blacks));
      material.SetFloat(_Shadows, flow.GetValue<float>(Shadows));
      material.SetFloat(_Mids, flow.GetValue<float>(Mids));
      material.SetFloat(_Highlights, flow.GetValue<float>(Highlights));
      material.SetFloat(_Whites, flow.GetValue<float>(Whites));
    }
  }
}
