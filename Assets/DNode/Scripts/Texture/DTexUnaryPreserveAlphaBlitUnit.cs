using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexUnaryPreserveAlphaBlitUnit : DTexUnaryBlitUnit {
    private int _PreserveAlpha = Shader.PropertyToID("_PreserveAlpha");
    [Inspectable] public bool PreserveAlpha = true;
    protected override void Definition() {
      base.Definition();
    }
    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetInt(_PreserveAlpha, PreserveAlpha ? 1 : 0);
    }
  }
}
