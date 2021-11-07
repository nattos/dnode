using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DTransform : DFrameObjectWithDataUnit<D3DTransform.Data> {
    public struct Data {
      public bool IsLocal;
      public DValue? Position;
      public DValue? Rotation;
      public DValue? Scale;
      public bool Relative;
    }

    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Vector3][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;
    [DoNotSerialize][PortLabelHidden][Vector3][ScaleRange] public ValueInput Scale;

    [Inspectable] public D3DSpaceType Space = D3DSpaceType.Local;
    [Inspectable] public bool Relative = true;

    protected override void Definition() {
      base.Definition();
      Position = ValueInput<DEvent>(nameof(Position), DEvent.CreateImmediate((DValue)Vector3.zero, triggered: false));
      Rotation = ValueInput<DEvent>(nameof(Rotation), DEvent.CreateImmediate((DValue)Vector3.zero, triggered: false));
      Scale = ValueInput<DEvent>(nameof(Scale), DEvent.CreateImmediate((DValue)Vector3.one, triggered: false));
    }

    protected override Data GetData(Flow flow, DFrameObject[] inputs) {
      return new Data {
        IsLocal = Space == D3DSpaceType.Local,
        Position = GetNullableDValueFromDEventInput(flow, Position),
        Rotation = GetNullableDValueFromDEventInput(flow, Rotation),
        Scale = GetNullableDValueFromDEventInput(flow, Scale),
        Relative = Relative,
      };
    }

    protected override void ApplyToObject(Data data, int row, GameObject input) {
      var transform = TransformComponent.GetOrAdd(input);
      if (!transform) {
        return;
      }
      if (data.Position != null) {
        Vector3 value = data.Position.Value.Vector3FromRow(row);
        if (data.Relative) {
          if (data.IsLocal) {
            transform.LocalPosition.Value += value;
          } else {
            transform.WorldPosition.Value += value;
          }
        } else {
          if (data.IsLocal) {
            transform.LocalPosition.Value = value;
          } else {
            transform.WorldPosition.Value = value;
          }
        }
      }
      if (data.Rotation != null) {
        Quaternion thisRotation = Quaternion.Euler(data.Rotation.Value.Vector3FromRow(row));
        if (data.Relative) {
          Quaternion originalRotation = data.IsLocal ? transform.LocalRotation.Value : transform.WorldRotation.Value;
          Quaternion newRotation = thisRotation * originalRotation;
          if (data.IsLocal) {
            transform.LocalRotation.Value = newRotation;
          } else {
            transform.WorldRotation.Value = newRotation;
          }
        } else {
          if (data.IsLocal) {
            transform.LocalRotation.Value = thisRotation;
          } else {
            transform.WorldRotation.Value = thisRotation;
          }
        }
      }
      if (data.Scale != null) {
        Vector3 value = data.Position.Value.Vector3FromRow(row, Vector3.one);
        if (data.Relative) {
          transform.LocalScale.Value = transform.LocalScale.Value.ElementMul(value);
        } else {
          transform.LocalScale.Value = value;
        }
      }
    }
  }
}
