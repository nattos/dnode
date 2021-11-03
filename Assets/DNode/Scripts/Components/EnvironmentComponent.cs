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
    }

    private static T GetOverride<T>(UnityEngine.Rendering.VolumeProfile profile) where T : UnityEngine.Rendering.VolumeComponent {
      if (profile.TryGet<T>(out T value)) {
        return value;
      }
      return null;
    }
  }
}
