using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DMathQuatToEuler : DArrayOperationBase<DMathQuatToEuler.Data> {
    public struct Data {
      public bool Radians;
    }

    [Inspectable] public bool Radians = false;

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = new Data {
        Radians = Radians,
      };
      return (input.Rows, 3);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int i = 0; i < result.Rows; ++i) {
        Quaternion quaternion = input.QuaternionFromRow(i);
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
