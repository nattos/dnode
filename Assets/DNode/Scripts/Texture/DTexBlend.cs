using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public enum DBlendMode {
    Blend,
    Add,
    Subtract,
    Multiply,
    Exposure,
  }

  public static class DBlendModeExtensions {
    public const string KeywordBlend = "BLEND_MODE_BLEND";
    public const string KeywordAdd = "BLEND_MODE_ADD";
    public const string KeywordSubtract = "BLEND_MODE_SUBTRACT";
    public const string KeywordMultiply = "BLEND_MODE_MULTIPLY";
    public const string KeywordExposure = "BLEND_MODE_EXPOSURE";

    public static string ShaderKeyword(this DBlendMode mode) {
      switch (mode) {
        default:
        case DBlendMode.Blend:
          return KeywordBlend;
        case DBlendMode.Add:
          return KeywordAdd;
        case DBlendMode.Subtract:
          return KeywordSubtract;
        case DBlendMode.Multiply:
          return KeywordMultiply;
        case DBlendMode.Exposure:
          return KeywordExposure;
      }
    }
  }

  public class DTexBlend : DTexBinaryBlitWithPrescaleUnit {
    private int _PrebiasB = Shader.PropertyToID("_PrebiasB");
    private int _Alpha = Shader.PropertyToID("_Alpha");

    [DoNotSerialize][PortLabelHidden][Scalar][OneRange][ShortEditor] public ValueInput Prebias;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Alpha;
    [DoNotSerialize] public ValueInput BlendMode;

    private DBlendMode _currentBlendMode = (DBlendMode)(-1);

    protected override void Definition() {
      base.Definition();
      Prebias = ValueInput<DValue>(nameof(Prebias), 0.0);
      Alpha = ValueInput<DValue>(nameof(Alpha), 0.5);
      BlendMode = ValueInput<DBlendMode>(nameof(BlendMode), DBlendMode.Blend);
    }
    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetFloat(_PrebiasB, flow.GetValue<DValue>(Prebias));
      material.SetFloat(_Alpha, flow.GetValue<DValue>(Alpha));
      DBlendMode newBlendMode = flow.GetValue<DBlendMode>(BlendMode);
      if (newBlendMode != _currentBlendMode) {
        material.DisableKeyword(_currentBlendMode.ShaderKeyword());
        material.EnableKeyword(newBlendMode.ShaderKeyword());
        _currentBlendMode = newBlendMode;
      }
      
    }
    protected override string ShaderPath => "Hidden/TexBlend";
  }
}
