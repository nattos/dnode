using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DNode {
  public class D3DCamera : DFrameUnit {
    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Vector3][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;

    [DoNotSerialize][PortLabelHidden][Scalar][Range(1, 179, 60)] public ValueInput FieldOfView;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(9, 300, 50)][LogScale] public ValueInput FocalLength;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultFarWorldRange, 0.3)][LogScale]  public ValueInput NearClip;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultFarWorldRange, 1000)][LogScale]  public ValueInput FarClip;
    [DoNotSerialize] public ValueInput ClearMode;
    [DoNotSerialize][Color] public ValueInput ClearColor;

    private bool _useFocalLength = true;
    [Serialize][Inspectable] public bool UseFocalLength {
      get => _useFocalLength;
      set {
        _useFocalLength = value;
        PortsChanged();
      }
    }

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Position = ValueInput<DEvent>(nameof(Position), DEvent.CreateImmediate(Vector3.zero, triggered: false));
      Rotation = ValueInput<DEvent>(nameof(Rotation), DEvent.CreateImmediate(Vector3.zero, triggered: false));

      if (_useFocalLength) {
        FocalLength = ValueInput<DEvent>(nameof(FocalLength), DEvent.CreateImmediate(50.0f, triggered: false));
      } else {
        FieldOfView = ValueInput<DEvent>(nameof(FieldOfView), DEvent.CreateImmediate(60.0f, triggered: false));
      }
      NearClip = ValueInput<DEvent>(nameof(NearClip), DEvent.CreateImmediate(0.3f, triggered: false));
      FarClip = ValueInput<DEvent>(nameof(FarClip), DEvent.CreateImmediate(1000.0f, triggered: false));
      ClearMode = ValueInput<HDAdditionalCameraData.ClearColorMode>(nameof(ClearMode), HDAdditionalCameraData.ClearColorMode.Sky);
      ClearColor = ValueInput<DEvent>(nameof(ClearColor), DEvent.CreateImmediate(Color.clear, triggered: false));

      DFrameArray<DFrameObject> ComputeFromFlow(Flow flow) {
        GameObject cameraObject = DScriptMachine.CurrentInstance.GlobalCamera.gameObject;
        var transform = TransformComponent.GetOrAdd(cameraObject);
        if (transform) {
          // TODO: Deal with world vs local coordinates.
          transform.LocalPosition.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, Position)?.Vector3FromRow(0));
          transform.LocalRotation.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, Rotation)?.QuaternionEulerFromRow(0));
        }

        var camera = CameraComponent.GetOrAdd(cameraObject);
        if (camera) {
          if (_useFocalLength) {
            float? focalLength = DFrameUnit.GetNullableDValueFromDEventInput(flow, FocalLength)?.FloatFromRow(0);
            if (focalLength != null) {
              camera.FieldOfView.Value = Camera.FocalLengthToFieldOfView(focalLength.Value, 24.0f); // 35mm => 24x36
            }
          } else {
            camera.FieldOfView.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, FieldOfView)?.FloatFromRow(0));
          }
          camera.NearClip.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, NearClip)?.FloatFromRow(0));
          camera.FarClip.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, FarClip)?.FloatFromRow(0));
          camera.ClearMode.Value = flow.GetValue<HDAdditionalCameraData.ClearColorMode>(ClearMode);
          camera.ClearColor.MaybeSetValue(DFrameUnit.GetNullableDValueFromDEventInput(flow, ClearColor)?.ColorFromRow(0));
        }

        return new DFrameArray<DFrameObject> { ValueArray = new[] { new DFrameObject { GameObject = cameraObject } } };
      }
      result = ValueOutput<DFrameArray<DFrameObject>>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
