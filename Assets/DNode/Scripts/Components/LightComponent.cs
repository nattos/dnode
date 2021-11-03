using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DNode {
  public class LightComponent : FrameComponentBase {
    public FrameComponentField<HDAdditionalLightData, HDLightType> Type;
    public FrameComponentField<HDAdditionalLightData, float> ShapeRadius;
    public FrameComponentField<HDAdditionalLightData, Color> FilterColor;
    public FrameComponentField<Light, float> TemperatureKelvin;
    public FrameComponentField<HDAdditionalLightData, float> IntensityLumens;
    public FrameComponentField<HDAdditionalLightData, float> Range;
    public FrameComponentField<HDAdditionalLightData, bool> VolumetricsEnabled;
    public FrameComponentField<HDAdditionalLightData, float> VolumetricsMultiplier;
    public FrameComponentField<HDAdditionalLightData, float> VolumetricsShadowDimmer;
    public FrameComponentField<HDAdditionalLightData, float> VolumetricsFadeDistance;

    protected override IEnumerable<IFrameComponentField> GetFields() {
      Light light = GetComponent<Light>();
      HDAdditionalLightData hdLight = GetComponent<HDAdditionalLightData>();
      yield return Type = new FrameComponentField<HDAdditionalLightData, HDLightType>(hdLight, self => self.type, (self, value) => self.type = value);
      yield return ShapeRadius = new FrameComponentField<HDAdditionalLightData, float>(hdLight, self => self.shapeRadius, (self, value) => self.shapeRadius = value);
      yield return FilterColor = new FrameComponentField<HDAdditionalLightData, Color>(hdLight, self => self.color, (self, value) => self.color = value);
      yield return TemperatureKelvin = new FrameComponentField<Light, float>(light, self => self.colorTemperature, (self, value) => self.colorTemperature = value);
      yield return IntensityLumens = new FrameComponentField<HDAdditionalLightData, float>(hdLight, self => self.intensity, (self, value) => self.intensity = value);
      yield return Range = new FrameComponentField<HDAdditionalLightData, float>(hdLight, self => self.range, (self, value) => self.range = value);
      yield return VolumetricsEnabled = new FrameComponentField<HDAdditionalLightData, bool>(hdLight, self => self.affectsVolumetric, (self, value) => self.affectsVolumetric = value);
      yield return VolumetricsMultiplier = new FrameComponentField<HDAdditionalLightData, float>(hdLight, self => self.volumetricDimmer, (self, value) => self.volumetricDimmer = value);
      yield return VolumetricsShadowDimmer = new FrameComponentField<HDAdditionalLightData, float>(hdLight, self => self.volumetricShadowDimmer, (self, value) => self.volumetricShadowDimmer = value);
      yield return VolumetricsFadeDistance = new FrameComponentField<HDAdditionalLightData, float>(hdLight, self => self.volumetricFadeDistance, (self, value) => self.volumetricFadeDistance = value);
    }

    public static LightComponent GetOrAdd(GameObject go) {
      if (!go) {
        return null;
      }
      var component = go.GetComponent<LightComponent>();
      if (!component) {
        if (!go.GetComponent<Light>()) {
          return null;
        }
        component = go.AddComponent<LightComponent>();
      }
      return component;
    }
  }
}
