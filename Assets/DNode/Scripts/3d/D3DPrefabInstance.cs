using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DPrefabInstance : DFrameUnit {
    [DoNotSerialize] public ValueInput Prefab;
    [DoNotSerialize][PortLabelHidden][Boolean] public ValueInput IsRigidbody;
    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Vector3][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;
    [DoNotSerialize][PortLabelHidden][Vector3][ScaleRange] public ValueInput Scale;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Prefab = ValueInput<GameObject>(nameof(Prefab), null).AllowsNull();
      IsRigidbody = ValueInput<DValue>(nameof(IsRigidbody), 0.0);
      Position = ValueInput<DValue>(nameof(Position), 0.0);
      Rotation = ValueInput<DValue>(nameof(Rotation), 0.0);
      Scale = ValueInput<DValue>(nameof(Scale), Vector3.one);

      DFrameArray<DFrameObject> ComputeFromFlow(Flow flow) {
        GameObject prefab = flow.GetValue<GameObject>(Prefab);

        DValue isRigidbody = flow.GetValue<DValue>(IsRigidbody);
        DValue position = flow.GetValue<DValue>(Position);
        DValue rotation = flow.GetValue<DValue>(Rotation);
        DValue scale = flow.GetValue<DValue>(Scale);
        int rows = Math.Max(isRigidbody.Rows, Math.Max(position.Rows, Math.Max(rotation.Rows, scale.Rows)));

        DMutableFrameArray<DFrameObject> result = new DMutableFrameArray<DFrameObject>(rows);
        for (int row = 0; row < rows; ++row) {
          (GameObject instance, _) = PrefabCache.InstantiatePrefab(this, null, prefab);
          TransformComponent transform = TransformComponent.GetOrAdd(instance);
          if (transform) {
            transform.IsRigidbody.Value = isRigidbody[row, 0] != 0;
            transform.InitialLocalPosition.Value = position.Vector3FromRow(row);
            transform.InitialLocalRotation.Value = Quaternion.Euler(rotation.Vector3FromRow(row));
            transform.InitialLocalScale.Value = scale.Vector3FromRow(row, Vector3.one);
          }
          result[row] = new DFrameObject { GameObject = instance };
        }
        return result.ToValue();
      }

      result = ValueOutput<DFrameArray<DFrameObject>>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
