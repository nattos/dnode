using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class MaterialComponent : FrameComponentBase {
    private int _BaseColorMap = Shader.PropertyToID("_BaseColorMap");
    private int _ExposureTexture = Shader.PropertyToID("_ExposureTexture");

    private int _RayTracing = Shader.PropertyToID("_RayTracing");
    private int _AlbedoAffectEmissive = Shader.PropertyToID("_AlbedoAffectEmissive");
    private int _EmissiveExposureWeight = Shader.PropertyToID("_EmissiveExposureWeight");
    private int _Metallic = Shader.PropertyToID("_Metallic");
    private int _Smoothness = Shader.PropertyToID("_Smoothness");

    private int _EmissiveColor = Shader.PropertyToID("_EmissiveColor");
    private int _BaseColor = Shader.PropertyToID("_BaseColor");

    public FrameComponentField<MaterialComponent, Color> BaseColor;
    public FrameComponentField<MaterialComponent, Texture> BaseTexture;
    public FrameComponentField<MaterialComponent, float> Metallic;
    public FrameComponentField<MaterialComponent, float> Smoothness;
    public FrameComponentField<MaterialComponent, Color> EmissionColor;
    public FrameComponentField<MaterialComponent, float> EmissionExposureWeight;
    public FrameComponentField<MaterialComponent, bool> EmissionMultiplyWithBase;

    private MaterialPropertyBlock _materialPropertyBlock;

    protected new void Awake() {
      _materialPropertyBlock = new MaterialPropertyBlock();
      base.Awake();
    }

    protected override void OnCommitFields(out bool wasChanged) {
      base.OnCommitFields(out wasChanged);
      if (wasChanged) {
        GetComponent<Renderer>().SetPropertyBlock(_materialPropertyBlock);
      }
    }

    protected override IEnumerable<IFrameComponentField> GetFields() {
      yield return BaseColor = MakeColorField(_BaseColor);
      yield return BaseTexture = MakeTextureField(_BaseColorMap);
      yield return Metallic = MakeFloatField(_Metallic);
      yield return Smoothness = MakeFloatField(_Smoothness);
      yield return EmissionColor = MakeColorField(_EmissiveColor);
      yield return EmissionExposureWeight = MakeFloatField(_EmissiveExposureWeight);
      yield return EmissionMultiplyWithBase = MakeBoolField(_AlbedoAffectEmissive);
    }

    private static int GetInt(MaterialComponent self, int propId) => self._materialPropertyBlock.GetInt(propId);
    private static void SetInt(MaterialComponent self, int propId, int value) => self._materialPropertyBlock.SetInt(propId, value);
    private static float GetFloat(MaterialComponent self, int propId) => self._materialPropertyBlock.GetFloat(propId);
    private static void SetFloat(MaterialComponent self, int propId, float value) => self._materialPropertyBlock.SetFloat(propId, value);
    private static Color GetColor(MaterialComponent self, int propId) => self._materialPropertyBlock.GetColor(propId);
    private static void SetColor(MaterialComponent self, int propId, Color value) => self._materialPropertyBlock.SetColor(propId, value);
    private static Texture GetTexture(MaterialComponent self, int propId) => self._materialPropertyBlock.GetTexture(propId);
    private static void SetTexture(MaterialComponent self, int propId, Texture value) => self._materialPropertyBlock.SetTexture(propId, value.OrNull() ?? Texture2D.whiteTexture);

    private static readonly Dictionary<int, (object getter, object setter, object getFromMaterial)> _propDelegates = new Dictionary<int, (object, object, object)>();

    private FrameComponentField<MaterialComponent, bool> MakeBoolField(int propId) {
      Func<MaterialComponent, bool> getter;
      Action<MaterialComponent, bool> setter;
      Func<Material, bool> getFromMaterial;
      if (_propDelegates.TryGetValue(propId, out var entry)) {
        getter = (Func<MaterialComponent, bool>)entry.getter;
        setter = (Action<MaterialComponent, bool>)entry.setter;
        getFromMaterial = (Func<Material, bool>)entry.getFromMaterial;
      } else {
        getter = self => GetFloat(self, propId) > 0.0;
        setter = (self, value) => SetFloat(self, propId, value ? 1.0f : 0.0f);
        getFromMaterial = mat => mat.HasFloat(propId) ? (mat.GetFloat(propId) > 0.0) : default;
        _propDelegates[propId] = (getter, setter, getFromMaterial);
      }
      return MakeField(getter, setter, getFromMaterial);
    }

    private FrameComponentField<MaterialComponent, int> MakeIntField(int propId) {
      Func<MaterialComponent, int> getter;
      Action<MaterialComponent, int> setter;
      Func<Material, int> getFromMaterial;
      if (_propDelegates.TryGetValue(propId, out var entry)) {
        getter = (Func<MaterialComponent, int>)entry.getter;
        setter = (Action<MaterialComponent, int>)entry.setter;
        getFromMaterial = (Func<Material, int>)entry.getFromMaterial;
      } else {
        getter = self => GetInt(self, propId);
        setter = (self, value) => SetInt(self, propId, value);
        getFromMaterial = mat => mat.HasInt(propId) ? mat.GetInt(propId) : default;
        _propDelegates[propId] = (getter, setter, getFromMaterial);
      }
      return MakeField(getter, setter, getFromMaterial);
    }

    private FrameComponentField<MaterialComponent, float> MakeFloatField(int propId) {
      Func<MaterialComponent, float> getter;
      Action<MaterialComponent, float> setter;
      Func<Material, float> getFromMaterial;
      if (_propDelegates.TryGetValue(propId, out var entry)) {
        getter = (Func<MaterialComponent, float>)entry.getter;
        setter = (Action<MaterialComponent, float>)entry.setter;
        getFromMaterial = (Func<Material, float>)entry.getFromMaterial;
      } else {
        getter = self => GetFloat(self, propId);
        setter = (self, value) => SetFloat(self, propId, value);
        getFromMaterial = mat => mat.HasFloat(propId) ? mat.GetFloat(propId) : default;
        _propDelegates[propId] = (getter, setter, getFromMaterial);
      }
      return MakeField(getter, setter, getFromMaterial);
    }

    private FrameComponentField<MaterialComponent, Color> MakeColorField(int propId) {
      Func<MaterialComponent, Color> getter;
      Action<MaterialComponent, Color> setter;
      Func<Material, Color> getFromMaterial;
      if (_propDelegates.TryGetValue(propId, out var entry)) {
        getter = (Func<MaterialComponent, Color>)entry.getter;
        setter = (Action<MaterialComponent, Color>)entry.setter;
        getFromMaterial = (Func<Material, Color>)entry.getFromMaterial;
      } else {
        getter = self => GetColor(self, propId);
        setter = (self, value) => SetColor(self, propId, value);
        getFromMaterial = mat => mat.HasColor(propId) ? mat.GetColor(propId) : default;
        _propDelegates[propId] = (getter, setter, getFromMaterial);
      }
      return MakeField(getter, setter, getFromMaterial);
    }

    private FrameComponentField<MaterialComponent, Texture> MakeTextureField(int propId) {
      Func<MaterialComponent, Texture> getter;
      Action<MaterialComponent, Texture> setter;
      Func<Material, Texture> getFromMaterial;
      if (_propDelegates.TryGetValue(propId, out var entry)) {
        getter = (Func<MaterialComponent, Texture>)entry.getter;
        setter = (Action<MaterialComponent, Texture>)entry.setter;
        getFromMaterial = (Func<Material, Texture>)entry.getFromMaterial;
      } else {
        getter = self => GetTexture(self, propId);
        setter = (self, value) => SetTexture(self, propId, value);
        getFromMaterial = mat => mat.HasTexture(propId) ? mat.GetTexture(propId) : default;
        _propDelegates[propId] = (getter, setter, getFromMaterial);
      }
      return MakeField(getter, setter, getFromMaterial);
    }

    private FrameComponentField<MaterialComponent, T> MakeField<T>(
        Func<MaterialComponent, T> getter, Action<MaterialComponent, T> setter, Func<Material, T> getFromMaterial) {
      Material material = GetComponent<Renderer>().sharedMaterial;
      T initialValue = getFromMaterial.Invoke(material);
      setter.Invoke(this, initialValue);
      return new FrameComponentField<MaterialComponent, T>(this, getter, setter);
    }

    private static readonly Lazy<Shader> _standardLitShader = new Lazy<Shader>(() => Shader.Find("HDRP/Lit"));

    public static MaterialComponent GetOrAdd(GameObject go) {
      if (!go) {
        return null;
      }
      var component = go.GetComponent<MaterialComponent>();
      if (!component) {
        Shader shader = go.GetComponent<Renderer>()?.sharedMaterial?.shader;
        if (shader != _standardLitShader.Value) {
          return null;
        }
        component = go.AddComponent<MaterialComponent>();
      }
      return component;
    }
  }
}
