using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexUnaryBlitUnit : DTexUnaryUnit {
    protected void OnDestroy() {
      UnityUtils.Destroy(_material);
      _material = null;
    }

    private Material _material;

    protected override void Compute(Flow flow, Texture input, RenderTexture output) {
      Material material = CreateMaterial();
      SetMaterialProperties(flow, material);
      Graphics.Blit(input, output, material);
    }

    protected Material CreateMaterial() {
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
  }
}
