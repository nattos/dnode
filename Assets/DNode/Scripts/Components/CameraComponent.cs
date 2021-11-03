using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DNode {
  public class CameraComponent : FrameComponentBase {
    public FrameComponentField<Camera, float> FieldOfView;
    public FrameComponentField<Camera, float> NearClip;
    public FrameComponentField<Camera, float> FarClip;
    public FrameComponentField<HDAdditionalCameraData, HDAdditionalCameraData.ClearColorMode> ClearMode;
    public FrameComponentField<HDAdditionalCameraData, Color> ClearColor;

    protected override IEnumerable<IFrameComponentField> GetFields() {
      Camera camera = GetComponent<Camera>();
      HDAdditionalCameraData cameraHdData = camera.GetComponent<HDAdditionalCameraData>();
      yield return FieldOfView = new FrameComponentField<Camera, float>(camera, self => self.fieldOfView, (self, value) => self.fieldOfView = value);
      yield return NearClip = new FrameComponentField<Camera, float>(camera, self => self.nearClipPlane, (self, value) => self.nearClipPlane = value);
      yield return FarClip = new FrameComponentField<Camera, float>(camera, self => self.farClipPlane, (self, value) => self.farClipPlane = value);
      yield return ClearMode = new FrameComponentField<HDAdditionalCameraData, HDAdditionalCameraData.ClearColorMode>(cameraHdData, self => self.clearColorMode, (self, value) => self.clearColorMode = value);
      yield return ClearColor = new FrameComponentField<HDAdditionalCameraData, Color>(cameraHdData, self => self.backgroundColorHDR, (self, value) => self.backgroundColorHDR = value);
    }

    public static CameraComponent GetOrAdd(GameObject go) {
      if (!go) {
        return null;
      }
      var component = go.GetComponent<CameraComponent>();
      if (!component) {
        if (!go.GetComponent<Camera>()) {
          return null;
        }
        component = go.AddComponent<CameraComponent>();
      }
      return component;
    }
  }
}
