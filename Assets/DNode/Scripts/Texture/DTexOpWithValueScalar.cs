using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexOpWithValueScalar : DTexUnaryPreserveAlphaBlitUnit {
    private int _Value = Shader.PropertyToID("_Value");
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Value;
    protected override void Definition() {
      base.Definition();
      Value = ValueInput<DValue>(nameof(Value), 0.0);
    }
    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetFloat(_Value, flow.GetValue<DValue>(Value));
    }
  }
}
