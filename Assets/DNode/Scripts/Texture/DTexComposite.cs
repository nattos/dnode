using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexComposite : DTexBinaryBlitWithPrescaleUnit {
    private int _PrebiasB = Shader.PropertyToID("_PrebiasB");
    private int _Alpha = Shader.PropertyToID("_Alpha");
    private int _ScaleBiasB = Shader.PropertyToID("_ScaleBiasB");

    [DoNotSerialize][PortLabelHidden][Scalar][OneRange][ShortEditor] public ValueInput Prebias;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Alpha;
    [DoNotSerialize] public ValueInput BlendMode;
    [DoNotSerialize][PortLabelHidden][Vector2][Range(0, 16.0, 1.0, 1.0)][ShortEditor] public ValueInput Scale;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange(0.5)][ShortEditor] public ValueInput Anchor;
    [DoNotSerialize][PortLabelHidden][Vector2][OneRange][ShortEditor] public ValueInput Offset;

    private DBlendMode _currentBlendMode = (DBlendMode)(-1);

    protected override void Definition() {
      base.Definition();
      Prebias = ValueInput<DValue>(nameof(Prebias), 0.0);
      Alpha = ValueInput<DValue>(nameof(Alpha), 0.5);
      BlendMode = ValueInput<DBlendMode>(nameof(BlendMode), DBlendMode.Blend);
      Scale = ValueInput<DValue>(nameof(Scale), Vector2.one);
      Anchor = ValueInput<DValue>(nameof(Anchor), Vector2.one * 0.5f);
      Offset = ValueInput<DValue>(nameof(Offset), Vector2.zero);
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

    protected override string ShaderPath => "Hidden/TexBlendScaled";

    protected override void Blit(Flow flow, Texture lhs, Texture rhs, RenderTexture output, Material material) {
      Vector2 scale = flow.GetValue<DValue>(Scale);
      Vector2 anchor = flow.GetValue<DValue>(Anchor);
      Vector2 offset = flow.GetValue<DValue>(Offset);

      Vector2 inputToScreenRatio = new Vector2(rhs.width * (float)output.height / (output.width * (float)rhs.height), 1.0f);

      Vector2 center = (anchor - Vector2.one * 0.5f).ElementMul(Vector2.one - scale).ElementMul(inputToScreenRatio) + Vector2.one * 0.5f;
      Vector2 size = scale.ElementMul(inputToScreenRatio);
      Vector2 min = center - size * 0.5f + offset.ElementMul(inputToScreenRatio);

      Vector2 invScale = Vector2.one.ElementDiv(Vector2.Max(Vector2.one * (float)UnityUtils.DefaultEpsilon, size));
      material.SetVector(_ScaleBiasB, new Vector4(invScale.x, invScale.y,
                                                  -min.x * invScale.x, -min.y * invScale.y));

      // TODO: This is inefficient as we render the composited region twice.
      Graphics.Blit(lhs, output);

      RenderTexture oldActive = RenderTexture.active;
      RenderTexture.active = output;
      GL.PushMatrix();
      GL.LoadOrtho();
      Rect screenRect = new Rect(min.x, min.y, size.x, size.y);
      Rect flippedScreenRect = screenRect;
      flippedScreenRect.y += flippedScreenRect.height;
      flippedScreenRect.height = -flippedScreenRect.height;
      Graphics.DrawTexture(flippedScreenRect, lhs, screenRect, 0, 0, 0, 0, material);
      GL.PopMatrix();
      RenderTexture.active = oldActive;
    }
  }
}
