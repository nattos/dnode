using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DTexGenBlitUnit : DTexGenUnit<DEmptyData> {
    protected void OnDestroy() {
      UnityUtils.Destroy(_material);
      _material = null;
    }

    private Material _material;

    protected override DEmptyData GetData(Flow flow) => default;

    protected override void Compute(Flow flow, DEmptyData _, RenderTexture output) {
      Compute(flow, output);
    }

    protected virtual void Compute(Flow flow, RenderTexture output) {
      Material material = CreateMaterial();
      SetMaterialProperties(flow, material);
      Graphics.Blit(null, output, material);
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
  }
}
