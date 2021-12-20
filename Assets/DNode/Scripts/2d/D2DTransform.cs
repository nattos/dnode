using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D2DTransform : DFrameObjectWithDataUnit<D2DTransform.Data> {
    public struct Data {
      public DValue? Position;
      public DValue? Depth;
      public DValue? FaceCamera;
      public Vector2 Anchor;
      public DValue? Rotation;
      public DValue? Scale;
      public bool Relative;
    }

    [DoNotSerialize][PortLabelHidden][Vector2][ScreenRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Scalar][WorldRange] public ValueInput Depth;
    [DoNotSerialize][PortLabelHidden][Boolean] public ValueInput FaceCamera;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange(0.5)] public ValueInput Anchor;
    [DoNotSerialize][PortLabelHidden][Vector2][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;
    [DoNotSerialize][PortLabelHidden][Vector2][ScaleRange] public ValueInput Scale;
    [DoNotSerialize] public ValueInput Relative;

    protected override void Definition() {
      base.Definition();
      Position = ValueInput<DEvent>(nameof(Position), default);
      Depth = ValueInput<DEvent>(nameof(Depth), default);
      FaceCamera = ValueInput<DEvent>(nameof(FaceCamera), DEvent.CreateImmediate(0.0, triggered: false));
      Anchor = ValueInput<DValue>(nameof(Anchor), new Vector2(0.5f, 0.5f));
      Rotation = ValueInput<DEvent>(nameof(Rotation), default);
      Scale = ValueInput<DEvent>(nameof(Scale), DEvent.CreateImmediate((DValue)1.0, triggered: false));
      Relative = ValueInput<bool>(nameof(Relative), true);
    }

    protected override Data GetData(Flow flow, DFrameObject[] inputs) {
      return new Data {
        Position = GetNullableDValueFromDEventInput(flow, Position),
        Depth = GetNullableDValueFromDEventInput(flow, Depth),
        FaceCamera = GetNullableDValueFromDEventInput(flow, FaceCamera),
        Anchor = flow.GetValue<DValue>(Anchor),
        Rotation = GetNullableDValueFromDEventInput(flow, Rotation),
        Scale = GetNullableDValueFromDEventInput(flow, Scale),
        Relative = flow.GetValue<bool>(Relative),
      };
    }

    protected override void ApplyToObject(Data data, int row, GameObject input) {
      var transform = TransformComponent.GetOrAdd(input);
      if (!transform) {
        return;
      }
      if (data.Position != null || data.Depth != null) {
        (Vector2 screenPos, float depth) = D2DUtils.WorldToScreenAndDepth(input.transform.position);
        Vector2 targetScreenPos;
        float targetDepth;

        if (data.Position != null) {
          Vector2 targetAnchorScreenPos = data.Position.Value.Vector2FromRow(row);
          if (data.Relative) {
            targetScreenPos = screenPos + targetAnchorScreenPos;
          } else {
            (Vector3 worldMin, Vector3 worldMax) = UnityUtils.GetWorldMeshBoundsMinMax(input);
            Vector2 screenMin = D2DUtils.WorldToScreen(worldMin);
            Vector2 screenMax = D2DUtils.WorldToScreen(worldMax);
            Vector2 anchorPos = UnityUtils.Lerp(screenMin, screenMax, data.Anchor);
            targetScreenPos = targetAnchorScreenPos + (screenPos - anchorPos);
          }
        } else {
          targetScreenPos = screenPos;
        }
        if (data.Depth != null) {
          float depthValue = (float)data.Depth.Value[row, 0];
          if (data.Relative) {
            targetDepth = depth + depthValue;
          } else {
            targetDepth = depthValue;
          }
        } else {
          targetDepth = depth;
        }
        Vector3 targetWorldPos = D2DUtils.ScreenToWorld(targetScreenPos, targetDepth);

        transform.LocalPosition.Value = targetWorldPos;
      }
      if (data.Rotation != null || data.FaceCamera != null) {
        float screenRotation = data.Rotation?.FloatFromRow(row) ?? 0.0f;
        Vector3 rotationEuler;
        if ((data.Rotation?.FloatFromRow(row) ?? 0.0f) != 0.0f) {
          rotationEuler = Quaternion.FromToRotation(Vector3.forward, transform.WorldPosition.Value - DScriptMachine.CurrentInstance.GlobalCamera.transform.position).eulerAngles;
        } else {
          rotationEuler = transform.WorldRotation.Value.eulerAngles;
        }
        Vector3 targetRotationEuler = rotationEuler;
        if (data.Relative) {
          targetRotationEuler.z += screenRotation;
        } else {
          targetRotationEuler.z = screenRotation;
        }
        transform.WorldRotation.Value = Quaternion.Euler(targetRotationEuler);
      }
      if (data.Scale != null) {
        float zScale = transform.LocalScale.Value.z;
        Vector2 screenValue = data.Position.Value.Vector2FromRow(row, Vector2.one);
        if (data.Relative) {
          Vector3 value = new Vector3(screenValue.x, screenValue.y, 1.0f);
          transform.LocalScale.Value = transform.LocalScale.Value.ElementMul(value);
        } else {
          Vector3 value = new Vector3(screenValue.x, screenValue.y, zScale);
          transform.LocalScale.Value = value;
        }
      }
    }
  }
}
