using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class TransformComponent : FrameComponentBase {
    public FrameComponentField<Transform, Vector3> LocalPosition;
    public FrameComponentField<Transform, Quaternion> LocalRotation;
    public FrameComponentField<Transform, Vector3> LocalScale;
    public FrameComponentField<Transform, Vector3> WorldPosition;
    public FrameComponentField<Transform, Quaternion> WorldRotation;

    protected override IEnumerable<IFrameComponentField> GetFields() {
      yield return LocalPosition = new FrameComponentField<Transform, Vector3>(transform, self => self.localPosition, (self, value) => self.localPosition = value);
      yield return LocalRotation = new FrameComponentField<Transform, Quaternion>(transform, self => self.localRotation, (self, value) => self.localRotation = value);
      yield return LocalScale = new FrameComponentField<Transform, Vector3>(transform, self => self.localScale, (self, value) => self.localScale = value);
      yield return WorldPosition = new FrameComponentField<Transform, Vector3>(transform, self => self.position, (self, value) => self.position = value);
      yield return WorldRotation = new FrameComponentField<Transform, Quaternion>(transform, self => self.rotation, (self, value) => self.rotation = value);
    }

    public static TransformComponent GetOrAdd(GameObject go) {
      if (!go) {
        return null;
      }
      var component = go.GetComponent<TransformComponent>();
      if (!component) {
        component = go.AddComponent<TransformComponent>();
      }
      return component;
    }
  }
}
