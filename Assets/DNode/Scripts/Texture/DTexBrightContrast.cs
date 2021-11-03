using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexBrightContrast : DTexUnaryPreserveAlphaBlitUnit {
    private int _Brightness = Shader.PropertyToID("_Brightness");
    private int _Contrast = Shader.PropertyToID("_Contrast");

    [DoNotSerialize][PortLabelHidden][Scalar][OneRange][ShortEditor] public ValueInput Brightness;
    [DoNotSerialize][PortLabelHidden][Scalar][OneRange][ShortEditor] public ValueInput Contrast;

    protected override void Definition() {
      base.Definition();

      Brightness = ValueInput<DValue>("Brightness", 0.0);
      Contrast = ValueInput<DValue>("Contrast", 0.0);
    }

    protected override string ShaderPath => "Hidden/TexBrightContrast";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetFloat(_Brightness, flow.GetValue<DValue>(Brightness));
      material.SetFloat(_Contrast, flow.GetValue<DValue>(Contrast));
    }
  }
}
