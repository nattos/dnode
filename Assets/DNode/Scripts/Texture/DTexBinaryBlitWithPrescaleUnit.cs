using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexBinaryBlitWithPrescaleUnit : DTexBinaryPreserveAlphaBlitUnit {
    private int _PrescaleB = Shader.PropertyToID("_PrescaleB");
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 1)][ShortEditor] public ValueInput PrescaleB;
    protected override void Definition() {
      base.Definition();
      PrescaleB = ValueInput<DValue>("PrescaleB", 1.0);
    }
    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetFloat(_PrescaleB, flow.GetValue<DValue>(PrescaleB));
    }
  }
}
