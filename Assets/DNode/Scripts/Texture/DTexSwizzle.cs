using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexSwizzle : DTexUnaryBlitUnit {
    private int _XBasis = Shader.PropertyToID("_XBasis");
    private int _YBasis = Shader.PropertyToID("_YBasis");
    private int _ZBasis = Shader.PropertyToID("_ZBasis");
    private int _WBasis = Shader.PropertyToID("_WBasis");

    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput XBasis;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput YBasis;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput ZBasis;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput WBasis;

    protected override void Definition() {
      base.Definition();

      XBasis = ValueInput<DValue>("XBasis", new Vector4(1, 0, 0, 0));
      YBasis = ValueInput<DValue>("YBasis", new Vector4(0, 1, 0, 0));
      ZBasis = ValueInput<DValue>("ZBasis", new Vector4(0, 0, 1, 0));
      WBasis = ValueInput<DValue>("WBasis", new Vector4(0, 0, 0, 1));
    }

    protected override string ShaderPath => "Hidden/TexSwizzle";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetVector(_XBasis, (Vector4)flow.GetValue<DValue>(XBasis));
      material.SetVector(_YBasis, (Vector4)flow.GetValue<DValue>(YBasis));
      material.SetVector(_ZBasis, (Vector4)flow.GetValue<DValue>(ZBasis));
      material.SetVector(_WBasis, (Vector4)flow.GetValue<DValue>(WBasis));
    }
  }
}
