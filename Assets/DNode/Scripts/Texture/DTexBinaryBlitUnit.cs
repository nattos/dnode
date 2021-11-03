using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexBinaryBlitUnit : DTexBinaryUnit {
    private int _SubTex = Shader.PropertyToID("_SubTex");

    protected void OnDestroy() {
      UnityUtils.Destroy(_material);
      _material = null;
    }

    private Material _material;

    protected override void Compute(Flow flow, Texture lhs, Texture rhs, RenderTexture output) {
      Material material = CreateMaterial();
      material.SetTexture(_SubTex, rhs);
      SetMaterialProperties(flow, material);
      Blit(flow, lhs, rhs, output, material);
    }

    private Material CreateMaterial() {
      if (_material != null) {
        return _material;
      }
      string shaderPath = ShaderPath;
      Shader shader = Shader.Find(shaderPath);
      if (!shader) {
        Debug.LogError($"Shader {shaderPath} was not found.");
        shader = Shader.Find("Hidden/BlitCopy");
      }
      _material = new Material(shader);
      return _material;
    }

    protected abstract string ShaderPath { get; }
    protected virtual void SetMaterialProperties(Flow flow, Material material) {}
    protected virtual void Blit(Flow flow, Texture lhs, Texture rhs, RenderTexture output, Material material) {
      Graphics.Blit(lhs, output, material);
    }
  }
}
