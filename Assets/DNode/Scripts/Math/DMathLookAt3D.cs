using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DMathLookAt3D : DArrayOperationBase<DMathLookAt3D.Data> {
    public struct Data {
      public DValue Target;
      public bool Radians;
      public bool AsQuaternion;
    }

    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange][ShortEditor] public ValueInput Target;
    [Inspectable] public bool Radians = false;
    [Inspectable] public bool AsQuaternion = false;

    protected override void Definition() {
      base.Definition();
      Target = ValueInput<DValue>("Target", 0.0);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = new Data {
        Target = flow.GetValue<DValue>(Target),
        Radians = Radians,
        AsQuaternion = AsQuaternion,
      };
      return (input.Rows, data.AsQuaternion ? 4 : 3);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int i = 0; i < result.Rows; ++i) {
        Vector3 from = input.Vector3FromRow(i);
        Vector3 to = data.Target.Vector3FromRow(i);
        Quaternion quaternion = Quaternion.FromToRotation(Vector3.forward, to - from);
        if (data.AsQuaternion) {
          result[i, 0] = quaternion.x;
          result[i, 1] = quaternion.y;
          result[i, 2] = quaternion.z;
          result[i, 3] = quaternion.w;
        } else {
          Vector3 euler = quaternion.eulerAngles;
          if (data.Radians) {
            euler *= Mathf.Deg2Rad;
          }
          result[i, 0] = euler.x;
          result[i, 1] = euler.y;
          result[i, 2] = euler.z;
        }
      }
    }
  }
}
