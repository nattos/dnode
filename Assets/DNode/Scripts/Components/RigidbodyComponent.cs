using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class RigidbodyComponent : FrameComponentBase {
    public FrameComponentField<Rigidbody, float> Mass;
    public FrameComponentField<Rigidbody, float> Drag;
    public FrameComponentField<Rigidbody, float> AngularDrag;
    public FrameComponentField<Rigidbody, bool> UseGravity;
    public FrameComponentField<Rigidbody, bool> IsKinematic;

    protected override IEnumerable<IFrameComponentField> GetFields() {
      Rigidbody body = GetComponent<Rigidbody>();
      yield return Mass = new FrameComponentField<Rigidbody, float>(body, self => self.mass, (self, value) => self.mass = value);
      yield return Drag = new FrameComponentField<Rigidbody, float>(body, self => self.drag, (self, value) => self.drag = value);
      yield return AngularDrag = new FrameComponentField<Rigidbody, float>(body, self => self.angularDrag, (self, value) => self.angularDrag = value);
      yield return UseGravity = new FrameComponentField<Rigidbody, bool>(body, self => self.useGravity, (self, value) => self.useGravity = value);
      yield return IsKinematic = new FrameComponentField<Rigidbody, bool>(body, self => self.isKinematic, (self, value) => self.isKinematic = value);
    }

    public static RigidbodyComponent GetOrAdd(GameObject go) {
      if (!go) {
        return null;
      }
      var component = go.GetComponent<RigidbodyComponent>();
      if (!component) {
        component = go.AddComponent<RigidbodyComponent>();
      }
      return component;
    }
  }
}
