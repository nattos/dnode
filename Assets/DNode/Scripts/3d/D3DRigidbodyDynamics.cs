using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DRigidbodyDynamics : DFrameObjectWithDataUnit<D3DRigidbodyDynamics.Data> {
    public struct Data {
      public DValue? ResetDynamics;
      public ForceMode VelocityMode;
      public DValue? AddVelocity;
      public DValue? AddAngularVelocity;
    }

    [DoNotSerialize][PortLabelHidden][Boolean] public ValueInput ResetDynamics;
    [DoNotSerialize] public ValueInput VelocityMode;
    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange] public ValueInput AddVelocity;
    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange] public ValueInput AddAngularVelocity;

    [Inspectable] public D3DSpaceType Space = D3DSpaceType.Local;
    [Inspectable] public bool Relative = true;

    protected override void Definition() {
      base.Definition();
      ResetDynamics = ValueInput<DEvent>(nameof(ResetDynamics), DEvent.CreateImmediate(0.0, triggered: false));
      VelocityMode = ValueInput<ForceMode>(nameof(VelocityMode), ForceMode.VelocityChange);
      AddVelocity = ValueInput<DEvent>(nameof(AddVelocity), DEvent.CreateImmediate((DValue)Vector3.zero, triggered: false));
      AddAngularVelocity = ValueInput<DEvent>(nameof(AddAngularVelocity), DEvent.CreateImmediate((DValue)Vector3.zero, triggered: false));
    }

    protected override Data GetData(Flow flow, DFrameObject[] inputs) {
      return new Data {
        ResetDynamics = GetNullableDValueFromDEventInput(flow, ResetDynamics),
        VelocityMode = flow.GetValue<ForceMode>(VelocityMode),
        AddVelocity = GetNullableDValueFromDEventInput(flow, AddVelocity),
        AddAngularVelocity = GetNullableDValueFromDEventInput(flow, AddAngularVelocity),
      };
    }

    protected override void ApplyToObject(Data data, int row, GameObject input) {
      Rigidbody rigidbody = input.GetComponent<Rigidbody>();
      if (!rigidbody) {
        return;
      }
      if (data.ResetDynamics != null) {
        if (data.ResetDynamics.Value[row, 0] != 0.0) {
          rigidbody.velocity = Vector3.zero;
          rigidbody.angularVelocity = Vector3.zero;
        }
      }
      if (data.AddVelocity != null) {
        Vector3 value = data.AddVelocity.Value.Vector3FromRow(row);
        rigidbody.AddForce(value, data.VelocityMode);
      }
      if (data.AddAngularVelocity != null) {
        Vector3 value = data.AddAngularVelocity.Value.Vector3FromRow(row);
        rigidbody.AddTorque(value, data.VelocityMode);
      }
    }
  }
}
