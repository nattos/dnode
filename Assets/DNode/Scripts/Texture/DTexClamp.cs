using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexClamp : DTexUnaryPreserveAlphaBlitUnit {
    private int _Min = Shader.PropertyToID("_Min");
    private int _Max = Shader.PropertyToID("_Max");

    [DoNotSerialize][PortLabelHidden][Color][ShortEditor] public ValueInput Min;
    [DoNotSerialize][PortLabelHidden][Color][ShortEditor] public ValueInput Max;

    protected override void Definition() {
      base.Definition();
      Min = ValueInput<DValue>("Max", Color.white);
      Max = ValueInput<DValue>("Min", Color.clear);
    }

    protected override string ShaderPath => "Hidden/TexClamp";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetColor(_Min, flow.GetValue<DValue>(Min));
      material.SetColor(_Max, flow.GetValue<DValue>(Max));
    }
  }
}
