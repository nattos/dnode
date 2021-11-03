using System;
using Unity.VisualScripting;

namespace DNode {
  public class DMathRotate2D : DArrayOperationBase<DMathRotate2D.Data> {
    public struct Data {
      public DValue Angle;
      public bool Radians;
    }

    [DoNotSerialize] public ValueInput Angle;
    [DoNotSerialize] public ValueInput Radians;

    protected override void Definition() {
      base.Definition();
      Angle = ValueInput<DValue>("Angle", 0.0);
      Radians = ValueInput<bool>("Radians", false);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = new Data {
        Angle = flow.GetValue<DValue>(Angle),
        Radians = flow.GetValue<bool>(Radians),
      };
      return (input.Rows, 2);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      if (data.Angle.Rows <= 1) {
        double angle = data.Angle[0, 0];
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        for (int i = 0; i < result.Rows; ++i) {
          double x = input[i, 0];
          double y = input[i, 1];
          result[i, 0] = x * cos - y * sin;
          result[i, 1] = x * sin + y * cos;
        }
      } else {
        for (int i = 0; i < result.Rows; ++i) {
          double x = input[i, 0];
          double y = input[i, 1];
          double angle = data.Angle[i, 0];
          double cos = Math.Cos(angle);
          double sin = Math.Sin(angle);
          result[i, 0] = x * cos - y * sin;
          result[i, 1] = x * sin + y * cos;
        }
      }
    }
  }
}
