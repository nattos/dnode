using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D2DGetTransform : DFrameObjectUnit {
    [DoNotSerialize] public ValueOutput Min;
    [DoNotSerialize] public ValueOutput Max;
    [DoNotSerialize] public ValueOutput MinMax;
    [DoNotSerialize] public ValueOutput Center;
    [DoNotSerialize] public ValueOutput Size;
    [DoNotSerialize] public ValueOutput Position;
    [DoNotSerialize] public ValueOutput Rotation;
    [DoNotSerialize] public ValueOutput Scale;

    protected override void Definition() {
      base.Definition();

      Func<Flow, (DValue min, DValue max)> computeRect = DNodeUtils.CachePerFrame(flow => {
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray.Length;
        DMutableValue resultMin = new DMutableValue(rows, 2);
        DMutableValue resultMax = new DMutableValue(rows, 2);
        for (int row = 0; row < rows; ++row) {
          (Vector3 worldMin, Vector3 worldMax) = UnityUtils.GetWorldMeshBoundsMinMax(input.ValueArray[row].GameObject);
          Vector2 screenMin = D2DUtils.WorldToScreen(worldMin);
          Vector2 screenMax = D2DUtils.WorldToScreen(worldMax);
          resultMin[row, 0] = screenMin.x;
          resultMin[row, 1] = screenMin.y;
          resultMax[row, 0] = screenMax.x;
          resultMax[row, 1] = screenMax.y;
        }
        return (resultMin.ToValue(), resultMax.ToValue());
      });
      Min = ValueOutput<DValue>("Min", flow => computeRect(flow).min);
      Max = ValueOutput<DValue>("Max", flow => computeRect(flow).max);
      MinMax = ValueOutput<DValue>("MinMax", DNodeUtils.CachePerFrame(flow => {
        (DValue min, DValue max) = computeRect(flow);
        DMutableValue result = new DMutableValue(min.Rows, 4);
        for (int row = 0; row < result.Rows; ++row) {
          result[row, 0] = min[row, 0];
          result[row, 1] = min[row, 1];
          result[row, 2] = max[row, 0];
          result[row, 3] = max[row, 1];
        }
        return result.ToValue();
      }));
      Center = ValueOutput<DValue>("Center", DNodeUtils.CachePerFrame(flow => {
        (DValue min, DValue max) = computeRect(flow);
        DMutableValue result = new DMutableValue(min.Rows, 2);
        for (int row = 0; row < result.Rows; ++row) {
          result[row, 0] = (min[row, 0] + max[row, 0]) * 0.5;
          result[row, 1] = (min[row, 1] + max[row, 1]) * 0.5;
        }
        return result.ToValue();
      }));
      Size = ValueOutput<DValue>("Size", flow => {
        (DValue min, DValue max) = computeRect(flow);
        DMutableValue result = new DMutableValue(min.Rows, 2);
        for (int row = 0; row < result.Rows; ++row) {
          result[row, 0] = max[row, 0] - min[row, 0];
          result[row, 1] = max[row, 1] - min[row, 1];
        }
        return result.ToValue();
      });
      Position = ValueOutput<DValue>("Position", DNodeUtils.CachePerFrame(flow => {
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray.Length;
        DMutableValue result = new DMutableValue(rows, 2);
        for (int row = 0; row < rows; ++row) {
          Vector2 screenPos = D2DUtils.WorldToScreen(input.ValueArray[row].GameObject.transform.position);
          result[row, 0] = screenPos.x;
          result[row, 1] = screenPos.y;
        }
        return result.ToValue();
      }));
      Rotation = ValueOutput<DValue>("Rotation", DNodeUtils.CachePerFrame(flow => {
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray.Length;
        DMutableValue result = new DMutableValue(rows, 1);
        for (int row = 0; row < rows; ++row) {
          // TODO: Handle rotated frames.
          float rotation = input.ValueArray[row].GameObject.transform.rotation.eulerAngles.z;
          result[row, 0] = rotation;
        }
        return result.ToValue();
      }));
      Scale = ValueOutput<DValue>("Scale", DNodeUtils.CachePerFrame(flow => {
        DFrameArray<DFrameObject> input = GetFrameObjects(flow, Input);
        int rows = input.ValueArray.Length;
        DMutableValue result = new DMutableValue(rows, 2);
        for (int row = 0; row < rows; ++row) {
          // TODO: Handle rotated frames.
          Vector3 scale = input.ValueArray[row].GameObject.transform.lossyScale;
          result[row, 0] = scale.x;
          result[row, 1] = scale.y;
        }
        return result.ToValue();
      }));
    }
    protected override DFrameArray<DFrameObject> Compute(Flow flow, DFrameObject[] inputs) => new DFrameArray<DFrameObject> { ValueArray = inputs };
  }
}
