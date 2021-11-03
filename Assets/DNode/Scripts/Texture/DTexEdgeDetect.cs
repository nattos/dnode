using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexEdgeDetect : DTexUnaryPreserveAlphaBlitUnit {
    private int _Width = Shader.PropertyToID("_Width");
    private int _XBasis = Shader.PropertyToID("_XBasis");
    private int _YBasis = Shader.PropertyToID("_YBasis");

    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange] public ValueInput Width;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange] public ValueInput XBasis;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange] public ValueInput YBasis;

    protected override void Definition() {
      base.Definition();
      Width = ValueInput<DValue>("Width", new Vector2(0.001f, 0.001f));
      XBasis = ValueInput<DValue>("XBasis", new Vector2(1.0f, 0.0f));
      YBasis = ValueInput<DValue>("YBasis", new Vector2(0.0f, 1.0f));
    }

    protected override string ShaderPath => "Hidden/TexEdgeDetect";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetVector(_Width, (Vector2)flow.GetValue<DValue>(Width));
      material.SetVector(_XBasis, (Vector2)flow.GetValue<DValue>(XBasis));
      material.SetVector(_YBasis, (Vector2)flow.GetValue<DValue>(YBasis));
    }
  }
}
