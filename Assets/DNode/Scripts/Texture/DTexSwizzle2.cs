using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexSwizzle2 : DTexBinaryBlitUnit {
    private int _XBasisA = Shader.PropertyToID("_XBasisA");
    private int _YBasisA = Shader.PropertyToID("_YBasisA");
    private int _ZBasisA = Shader.PropertyToID("_ZBasisA");
    private int _WBasisA = Shader.PropertyToID("_WBasisA");
    private int _XBasisB = Shader.PropertyToID("_XBasisB");
    private int _YBasisB = Shader.PropertyToID("_YBasisB");
    private int _ZBasisB = Shader.PropertyToID("_ZBasisB");
    private int _WBasisB = Shader.PropertyToID("_WBasisB");

    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput XBasisA;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput YBasisA;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput ZBasisA;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput WBasisA;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput XBasisB;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput YBasisB;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput ZBasisB;
    [DoNotSerialize][PortLabelHidden][Vector4][ZeroOneRange][ShortEditor] public ValueInput WBasisB;

    protected override void Definition() {
      base.Definition();

      XBasisA = ValueInput<DValue>("XBasisA", new Vector4(1, 0, 0, 0));
      YBasisA = ValueInput<DValue>("YBasisA", new Vector4(0, 1, 0, 0));
      ZBasisA = ValueInput<DValue>("ZBasisA", new Vector4(0, 0, 1, 0));
      WBasisA = ValueInput<DValue>("WBasisA", new Vector4(0, 0, 0, 1));
      XBasisB = ValueInput<DValue>("XBasisB", new Vector4(1, 0, 0, 0));
      YBasisB = ValueInput<DValue>("YBasisB", new Vector4(0, 1, 0, 0));
      ZBasisB = ValueInput<DValue>("ZBasisB", new Vector4(0, 0, 1, 0));
      WBasisB = ValueInput<DValue>("WBasisB", new Vector4(0, 0, 0, 1));
    }

    protected override string ShaderPath => "Hidden/TexSwizzle2";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetVector(_XBasisA, (Vector4)flow.GetValue<DValue>(XBasisA));
      material.SetVector(_YBasisA, (Vector4)flow.GetValue<DValue>(YBasisA));
      material.SetVector(_ZBasisA, (Vector4)flow.GetValue<DValue>(ZBasisA));
      material.SetVector(_WBasisA, (Vector4)flow.GetValue<DValue>(WBasisA));
      material.SetVector(_XBasisB, (Vector4)flow.GetValue<DValue>(XBasisB));
      material.SetVector(_YBasisB, (Vector4)flow.GetValue<DValue>(YBasisB));
      material.SetVector(_ZBasisB, (Vector4)flow.GetValue<DValue>(ZBasisB));
      material.SetVector(_WBasisB, (Vector4)flow.GetValue<DValue>(WBasisB));
    }
  }
}
