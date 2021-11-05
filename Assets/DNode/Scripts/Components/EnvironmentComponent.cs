using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class EnvironmentComponent : FrameComponentBase {
    public FrameComponentField<UnityEngine.Rendering.Volume, Texture> BackgroundTexture;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> BackgroundTextureAlpha;
    public FrameComponentField<UnityEngine.Rendering.Volume, Color> SkyGradientTopColor;
    public FrameComponentField<UnityEngine.Rendering.Volume, Color> SkyGradientMiddleColor;
    public FrameComponentField<UnityEngine.Rendering.Volume, Color> SkyGradientBottomColor;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> SkyGradientExposure;
  
    public FrameComponentField<UnityEngine.Rendering.Volume, bool> FogEnabled;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> FogAttenuationDistance;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> FogBaseHeight;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> FogMaxHeight;

    public FrameComponentField<UnityEngine.Rendering.Volume, float> ExposureCompensation;

    public FrameComponentField<UnityEngine.Rendering.Volume, float> MotionBlurIntensity;

    public FrameComponentField<UnityEngine.Rendering.Volume, UnityEngine.Rendering.HighDefinition.FilmGrainLookup> FilmGrainType;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> FilmGrainIntensity;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> FilmGrainResponse;

    public FrameComponentField<UnityEngine.Rendering.Volume, float> BloomThreshold;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> BloomIntensity;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> BloomScatter;
    public FrameComponentField<UnityEngine.Rendering.Volume, Color> BloomTint;

    public FrameComponentField<UnityEngine.Rendering.Volume, float> AmbientOcclusionIntensity;
    public FrameComponentField<UnityEngine.Rendering.Volume, float> AmbientOcclusionRadius;

    protected override IEnumerable<IFrameComponentField> GetFields() {
      UnityEngine.Rendering.Volume volume = GetComponent<UnityEngine.Rendering.Volume>();

      FrameComponentField<UnityEngine.Rendering.Volume, TValue> Field<TVolumeComponent, TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<TVolumeComponent, TValue> getter, Action<TVolumeComponent, TValue> setter) where TVolumeComponent : UnityEngine.Rendering.VolumeComponent {
        return new FrameComponentField<UnityEngine.Rendering.Volume, TValue>(innerVolume,
            self => getter.Invoke(GetOverride<TVolumeComponent>(self.profile)),
            (self, value) => setter.Invoke(GetOverride<TVolumeComponent>(self.profile), value));
      }
      FrameComponentField<UnityEngine.Rendering.Volume, TValue> BackgroundTextureSkySettingsField<TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<BackgroundTextureSkySettings, TValue> getter, Action<BackgroundTextureSkySettings, TValue> setter) {
        return Field<BackgroundTextureSkySettings, TValue>(innerVolume, getter, setter);
      }

      yield return BackgroundTexture = BackgroundTextureSkySettingsField(volume, self => self.Texture.value, (self, value) => self.Texture.value = value);
      yield return BackgroundTextureAlpha = BackgroundTextureSkySettingsField(volume, self => self.TextureAlpha.value, (self, value) => self.TextureAlpha.value = value);
      yield return SkyGradientTopColor = BackgroundTextureSkySettingsField(volume, self => self.top.value, (self, value) => self.top.value = value);
      yield return SkyGradientMiddleColor = BackgroundTextureSkySettingsField(volume, self => self.middle.value, (self, value) => self.middle.value = value);
      yield return SkyGradientBottomColor = BackgroundTextureSkySettingsField(volume, self => self.bottom.value, (self, value) => self.bottom.value = value);
      yield return SkyGradientExposure = BackgroundTextureSkySettingsField(volume, self => self.exposure.value, (self, value) => self.exposure.value = value);

      FrameComponentField<UnityEngine.Rendering.Volume, TValue> FogSettingsField<TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<UnityEngine.Rendering.HighDefinition.Fog, TValue> getter, Action<UnityEngine.Rendering.HighDefinition.Fog, TValue> setter) {
        return Field<UnityEngine.Rendering.HighDefinition.Fog, TValue>(innerVolume, getter, setter);
      }

      yield return FogEnabled = FogSettingsField(volume, self => self.enabled.value, (self, value) => self.enabled.value = value);
      yield return FogAttenuationDistance = FogSettingsField(volume, self => self.depthExtent.value, (self, value) => self.depthExtent.value = value);
      yield return FogBaseHeight = FogSettingsField(volume, self => self.baseHeight.value, (self, value) => self.baseHeight.value = value);
      yield return FogMaxHeight = FogSettingsField(volume, self => self.maximumHeight.value, (self, value) => self.maximumHeight.value = value);

      FrameComponentField<UnityEngine.Rendering.Volume, TValue> ExposureSettingsField<TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<UnityEngine.Rendering.HighDefinition.Exposure, TValue> getter, Action<UnityEngine.Rendering.HighDefinition.Exposure, TValue> setter) {
        return Field<UnityEngine.Rendering.HighDefinition.Exposure, TValue>(innerVolume, getter, setter);
      }

      yield return ExposureCompensation = ExposureSettingsField(volume, self => self.compensation.value, (self, value) => self.compensation.value = value);

      FrameComponentField<UnityEngine.Rendering.Volume, TValue> MotionBlurSettingsField<TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<UnityEngine.Rendering.HighDefinition.MotionBlur, TValue> getter, Action<UnityEngine.Rendering.HighDefinition.MotionBlur, TValue> setter) {
        return Field<UnityEngine.Rendering.HighDefinition.MotionBlur, TValue>(innerVolume, getter, setter);
      }

      yield return MotionBlurIntensity = MotionBlurSettingsField(volume, self => self.intensity.value, (self, value) => self.intensity.value = value);

      FrameComponentField<UnityEngine.Rendering.Volume, TValue> FilmGrainSettingsField<TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<UnityEngine.Rendering.HighDefinition.FilmGrain, TValue> getter, Action<UnityEngine.Rendering.HighDefinition.FilmGrain, TValue> setter) {
        return Field<UnityEngine.Rendering.HighDefinition.FilmGrain, TValue>(innerVolume, getter, setter);
      }

      yield return FilmGrainType = FilmGrainSettingsField(volume, self => self.type.value, (self, value) => self.type.value = value);
      yield return FilmGrainIntensity = FilmGrainSettingsField(volume, self => self.intensity.value, (self, value) => self.intensity.value = value);
      yield return FilmGrainResponse = FilmGrainSettingsField(volume, self => self.response.value, (self, value) => self.response.value = value);

      FrameComponentField<UnityEngine.Rendering.Volume, TValue> BloomSettingsField<TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<UnityEngine.Rendering.HighDefinition.Bloom, TValue> getter, Action<UnityEngine.Rendering.HighDefinition.Bloom, TValue> setter) {
        return Field<UnityEngine.Rendering.HighDefinition.Bloom, TValue>(innerVolume, getter, setter);
      }

      yield return BloomThreshold = BloomSettingsField(volume, self => self.threshold.value, (self, value) => self.threshold.value = value);
      yield return BloomIntensity = BloomSettingsField(volume, self => self.intensity.value, (self, value) => self.intensity.value = value);
      yield return BloomScatter = BloomSettingsField(volume, self => self.scatter.value, (self, value) => self.scatter.value = value);
      yield return BloomTint = BloomSettingsField(volume, self => self.tint.value, (self, value) => self.tint.value = value);

      FrameComponentField<UnityEngine.Rendering.Volume, TValue> AmbientOcclusionSettingsField<TValue>(
          UnityEngine.Rendering.Volume innerVolume,
          Func<UnityEngine.Rendering.HighDefinition.AmbientOcclusion, TValue> getter, Action<UnityEngine.Rendering.HighDefinition.AmbientOcclusion, TValue> setter) {
        return Field<UnityEngine.Rendering.HighDefinition.AmbientOcclusion, TValue>(innerVolume, getter, setter);
      }

      yield return AmbientOcclusionIntensity = AmbientOcclusionSettingsField(volume, self => self.intensity.value, (self, value) => self.intensity.value = value);
      yield return AmbientOcclusionRadius = AmbientOcclusionSettingsField(volume, self => self.radius.value, (self, value) => self.radius.value = value);
    }

    private static T GetOverride<T>(UnityEngine.Rendering.VolumeProfile profile) where T : UnityEngine.Rendering.VolumeComponent {
      if (profile.TryGet<T>(out T value)) {
        return value;
      }
      return null;
    }
  }
}
