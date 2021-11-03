using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DColorExposure : DArrayOperationBase<DColorExposure.Data> {
    public struct Data {
      public DValue Exposure;
    }

    [DoNotSerialize][PortLabelHidden][Scalar][Range(-16, 16, 0)] public ValueInput Exposure;

    protected override void Definition() {
      base.Definition();
      Exposure = ValueInput<DValue>("Exposure", 0.0);
    }

    protected override (int rows, int cols) GetOutputSize(Flow flow, DValue input, out Data data) {
      data = new Data {
        Exposure = flow.GetValue<DValue>(Exposure),
      };
      int rows = Math.Max(input.Rows, data.Exposure.Rows);
      return (rows, 4);
    }

    protected override void FillRows(Data data, DMutableValue result, DValue input) {
      int columns = result.Columns;
      if (data.Exposure.Rows <= 1) {
        float exposure = (float)data.Exposure[0, 0];
        float gain = Mathf.Pow(2, exposure);
        for (int i = 0; i < result.Rows; ++i) {
          Color inputColor = input.ColorFromRow(i, Color.black);
          Color outputColor = inputColor * gain;
          result[i, 0] = outputColor.r;
          result[i, 1] = outputColor.g;
          result[i, 2] = outputColor.b;
          result[i, 3] = outputColor.a;
        }
      } else {
        for (int i = 0; i < result.Rows; ++i) {
          float exposure = (float)data.Exposure[i, 0];
          float gain = Mathf.Pow(2, exposure);
          Color inputColor = input.ColorFromRow(i, Color.black);
          Color outputColor = inputColor * gain;
          result[i, 0] = outputColor.r;
          result[i, 1] = outputColor.g;
          result[i, 2] = outputColor.b;
          result[i, 3] = outputColor.a;
        }
      }
    }
  }
}
