using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DNode {
  public class BackgroundTextureSkyRenderer : SkyRenderer {
    Material _gradientSkyMaterial; // Renders a cubemap into a render texture (can be cube or 2D)
    MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();

    readonly int _Texture2D = Shader.PropertyToID("_Texture2D");
    readonly int _TextureAlpha = Shader.PropertyToID("_TextureAlpha");
    readonly int _SkyIntensity = Shader.PropertyToID("_SkyIntensity");
    readonly int _PixelCoordToViewDirWS = Shader.PropertyToID("_PixelCoordToViewDirWS");
    readonly int _GradientBottom = Shader.PropertyToID("_GradientBottom");
    readonly int _GradientMiddle = Shader.PropertyToID("_GradientMiddle");
    readonly int _GradientTop = Shader.PropertyToID("_GradientTop");
    readonly int _GradientDiffusion = Shader.PropertyToID("_GradientDiffusion");

    public BackgroundTextureSkyRenderer() {
      SupportDynamicSunLight = false;
    }

    public override void Build() {
      _gradientSkyMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("BackgroundTextureSkyShader"));
    }

    public override void Cleanup() {
      CoreUtils.Destroy(_gradientSkyMaterial);
    }

    public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk) {
      var gradientSky = builtinParams.skySettings as BackgroundTextureSkySettings;
      _gradientSkyMaterial.SetTexture(_Texture2D, gradientSky.Texture.value.OrNull() ?? Texture2D.blackTexture);
      _gradientSkyMaterial.SetFloat(_TextureAlpha, gradientSky.TextureAlpha.value);
      _gradientSkyMaterial.SetColor(_GradientBottom, gradientSky.bottom.value);
      _gradientSkyMaterial.SetColor(_GradientMiddle, gradientSky.middle.value);
      _gradientSkyMaterial.SetColor(_GradientTop, gradientSky.top.value);
      _gradientSkyMaterial.SetFloat(_GradientDiffusion, gradientSky.gradientDiffusion.value);
      _gradientSkyMaterial.SetFloat(_SkyIntensity, GetSkyIntensity(gradientSky, builtinParams.debugSettings));

      // This matrix needs to be updated at the draw call frequency.
      _propertyBlock.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);

      CoreUtils.DrawFullScreen(builtinParams.commandBuffer, _gradientSkyMaterial, _propertyBlock, renderForCubemap ? 0 : 1);
    }
  }
}
