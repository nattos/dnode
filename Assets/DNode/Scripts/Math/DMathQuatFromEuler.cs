using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DMathQuatFromEuler : DArrayOperationBase<DMathQuatFromEuler.Data> {
    public struct Data {
      public bool Radians;
    }

    [Inspectable] public bool Radians = false;

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = new Data {
        Radians = Radians,
      };
      return (input.Rows, 4);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int i = 0; i < result.Rows; ++i) {
        Vector3 euler = input.Vector3FromRow(i);
        if (data.Radians) {
          euler *= Mathf.Rad2Deg;
        }
        Quaternion quaternion = Quaternion.Euler(euler);
        result[i, 0] = quaternion.x;
        result[i, 1] = quaternion.y;
        result[i, 2] = quaternion.z;
        result[i, 3] = quaternion.w;
      }
    }
  }
}
