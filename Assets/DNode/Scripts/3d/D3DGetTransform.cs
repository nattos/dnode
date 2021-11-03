using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DGetTransform : DFrameObjectUnit {
    [Inspectable] public D3DSpaceType Space = D3DSpaceType.Local;
    [DoNotSerialize] public ValueOutput Position;
    [DoNotSerialize] public ValueOutput Rotation;
    [DoNotSerialize] public ValueOutput RotationQuaternion;
    [DoNotSerialize] public ValueOutput Scale;

    protected override void Definition() {
      base.Definition();

      Position = ValueOutput<DValue>("Position", flow => {
        bool isLocal = Space == D3DSpaceType.Local;
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray?.Length ?? 0;
        DMutableValue result = new DMutableValue(rows, 3);
        for (int row = 0; row < rows; ++row) {
          Transform transform = input.ValueArray[row].GameObject.transform;
          Vector3 value = isLocal ? transform.localPosition : transform.position;
          result[row, 0] = value.x;
          result[row, 1] = value.y;
          result[row, 2] = value.z;
        }
        return result.ToValue();
      });
      Rotation = ValueOutput<DValue>("Rotation", DNodeUtils.CachePerFrame(flow => {
        bool isLocal = Space == D3DSpaceType.Local;
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray?.Length ?? 0;
        DMutableValue result = new DMutableValue(rows, 3);
        for (int row = 0; row < rows; ++row) {
          Transform transform = input.ValueArray[row].GameObject.transform;
          Quaternion quaternion = isLocal ? transform.localRotation : transform.rotation;
          Vector3 value = quaternion.eulerAngles;
          result[row, 0] = value.x;
          result[row, 1] = value.y;
          result[row, 2] = value.z;
        }
        return result.ToValue();
      }));
      RotationQuaternion = ValueOutput<DValue>("Quaternion", DNodeUtils.CachePerFrame(flow => {
        bool isLocal = Space == D3DSpaceType.Local;
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray?.Length ?? 0;
        DMutableValue result = new DMutableValue(rows, 4);
        for (int row = 0; row < rows; ++row) {
          Transform transform = input.ValueArray[row].GameObject.transform;
          Quaternion value = isLocal ? transform.localRotation : transform.rotation;
          result[row, 0] = value.x;
          result[row, 1] = value.y;
          result[row, 2] = value.z;
          result[row, 3] = value.w;
        }
        return result.ToValue();
      }));
      Scale = ValueOutput<DValue>("Scale", DNodeUtils.CachePerFrame(flow => {
        bool isLocal = Space == D3DSpaceType.Local;
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray?.Length ?? 0;
        DMutableValue result = new DMutableValue(rows, 3);
        for (int row = 0; row < rows; ++row) {
          Transform transform = input.ValueArray[row].GameObject.transform;
          Vector3 value = isLocal ? transform.localScale : transform.lossyScale;
          result[row, 0] = value.x;
          result[row, 1] = value.y;
          result[row, 2] = value.z;
        }
        return result.ToValue();
      }));
    }
    protected override DFrameArray<DFrameObject> Compute(Flow flow, DFrameObject[] inputs) => new DFrameArray<DFrameObject> { ValueArray = inputs };
  }
}
