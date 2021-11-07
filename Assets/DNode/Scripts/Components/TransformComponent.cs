using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class TransformComponent : FrameComponentBase {
    public FrameComponentField<TransformComponent, bool> IsRigidbody;
    public FrameComponentField<TransformComponent, Vector3> InitialLocalPosition;
    public FrameComponentField<TransformComponent, Quaternion> InitialLocalRotation;
    public FrameComponentField<TransformComponent, Vector3> InitialLocalScale;
    public FrameComponentField<TransformComponent, Vector3> LocalPosition;
    public FrameComponentField<TransformComponent, Quaternion> LocalRotation;
    public FrameComponentField<TransformComponent, Vector3> LocalScale;
    public FrameComponentField<TransformComponent, Vector3> WorldPosition;
    public FrameComponentField<TransformComponent, Quaternion> WorldRotation;

    private Transform _transform;
    private Rigidbody _rigidbody;

    private bool _isRigidbody = false;
    private bool _wasRigidbody = false;

    protected override IEnumerable<IFrameComponentField> GetFields() {
      Transform thisTransform = transform;
      _transform = thisTransform;
      _rigidbody = GetComponent<Rigidbody>().OrNull();
      yield return IsRigidbody = new FrameComponentField<TransformComponent, bool>(this, self => self._isRigidbody, (self, value) => self._isRigidbody = value);
      yield return InitialLocalPosition = new FrameComponentField<TransformComponent, Vector3>(this, self => self._transform.localPosition, SetIfNotRigidbody<Vector3>((self, value) => { self._transform.localPosition = value; ResetVelocity(self); }), useChangeCheck: false);
      yield return InitialLocalRotation = new FrameComponentField<TransformComponent, Quaternion>(this, self => self._transform.localRotation, SetIfNotRigidbody<Quaternion>((self, value) => { self._transform.localRotation = value; ResetAngularVelocity(self); }), useChangeCheck: false);
      yield return InitialLocalScale = new FrameComponentField<TransformComponent, Vector3>(this, self => self._transform.localScale, SetIfNotRigidbody<Vector3>((self, value) => self._transform.localScale = value), useChangeCheck: false);
      yield return LocalPosition = new FrameComponentField<TransformComponent, Vector3>(this, self => self._transform.localPosition, (self, value) => { self._transform.localPosition = value; ResetVelocity(self); }, useChangeCheckPredicate: WasNotRigidbodyCheck, shouldResetPredicate: WasNotRigidbodyCheck);
      yield return LocalRotation = new FrameComponentField<TransformComponent, Quaternion>(this, self => self._transform.localRotation, (self, value) => { self._transform.localRotation = value; ResetAngularVelocity(self); }, useChangeCheckPredicate: WasNotRigidbodyCheck, shouldResetPredicate: WasNotRigidbodyCheck);
      yield return LocalScale = new FrameComponentField<TransformComponent, Vector3>(this, self => self._transform.localScale, (self, value) => self._transform.localScale = value);
      yield return WorldPosition = new FrameComponentField<TransformComponent, Vector3>(this, self => self._transform.position, (self, value) => { self._transform.position = value; ResetVelocity(self); }, useChangeCheckPredicate: WasNotRigidbodyCheck, shouldResetPredicate: WasNotRigidbodyCheck);
      yield return WorldRotation = new FrameComponentField<TransformComponent, Quaternion>(this, self => self._transform.rotation, (self, value) => { self._transform.rotation = value; ResetAngularVelocity(self); }, useChangeCheckPredicate: WasNotRigidbodyCheck, shouldResetPredicate: WasNotRigidbodyCheck);
    }

    private static void ResetVelocity(TransformComponent self) {
      if (!self._rigidbody) {
        return;
      }
      self._rigidbody.velocity = Vector3.zero;
    }

    private static void ResetAngularVelocity(TransformComponent self) {
      if (!self._rigidbody) {
        return;
      }
      self._rigidbody.angularVelocity = Vector3.zero;
    }

    private static bool WasNotRigidbodyCheck(TransformComponent self) {
      bool requiresOverride = self._wasRigidbody && !(self._rigidbody?.isKinematic ?? true);
      return !requiresOverride;
    }

    private static Action<TransformComponent, T> SetIfNotRigidbody<T>(Action<TransformComponent, T> setter) {
      return (self, value) => {
        if (self._wasRigidbody) {
          return;
        }
        setter.Invoke(self, value);
      };
    }

    public override void OnStartFrame() {
      base.OnStartFrame();
      _wasRigidbody = _isRigidbody;
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
