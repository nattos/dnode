using System;
using Unity.VisualScripting;

namespace DNode {
  public class DMathFromPolar : DArrayOperationBase<DMathFromPolar.Data> {
    public struct Data {
      public bool Radians;
    }

    [Inspectable] public bool Radians = false;

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = new Data {
        Radians = Radians,
      };
      return (input.Rows, 2);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      for (int i = 0; i < result.Rows; ++i) {
        double magnitude = input[i, 0];
        double angle = input[i, 1];
        if (!data.Radians) {
          angle = angle / 360 * (Math.PI * 2);
        }
        double x = Math.Cos(angle) * magnitude;
        double y = Math.Sin(angle) * magnitude;
        result[i, 0] = x;
        result[i, 1] = y;
      }
    }
  }
}
