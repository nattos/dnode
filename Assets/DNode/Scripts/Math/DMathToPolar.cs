using System;
using Unity.VisualScripting;

namespace DNode {
  public class DMathToPolar : DArrayOperationBase<DMathToPolar.Data> {
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
        double x = input[i, 0];
        double y = input[i, 1];
        double angle = Math.Atan2(y, x);
        if (!data.Radians) {
          angle = angle * 360 / (Math.PI * 2);
        }
        double magnitude = Math.Sqrt(x * x + y * y);
        result[i, 0] = magnitude;
        result[i, 1] = angle;
      }
    }
  }
}
