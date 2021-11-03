using System;
using Unity.VisualScripting;

namespace DNode {
  public class DRemapRange : DUnaryDataScalarOperationBase<DRemapRange.Data> {
    public struct Data {
      public double InputMin;
      public double InputMax;
      public double InputRange;
      public double OutputMin;
      public double OutputMax;
    }

    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput InputMin;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput InputMax;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput OutputMin;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput OutputMax;

    protected override void Definition() {
      base.Definition();

      InputMin = ValueInput<DValue>(nameof(InputMin), 0.0);
      InputMax = ValueInput<DValue>(nameof(InputMax), 1.0);
      OutputMin = ValueInput<DValue>(nameof(OutputMin), 0.0);
      OutputMax = ValueInput<DValue>(nameof(OutputMax), 1.0);
    }

    protected override Data GetData(Flow flow, DValue input) {
      double inputMin = flow.GetValue<DValue>(InputMin);
      double inputMax = flow.GetValue<DValue>(InputMax);
      double inputRawRange = inputMax - inputMin;
      double inputRange = Math.Abs(inputRawRange) < 0.0000001 ? 1.0 : inputRawRange;
      return new Data {
        InputMin = inputMin,
        InputMax = inputMax,
        InputRange = inputRange,
        OutputMin = flow.GetValue<DValue>(OutputMin),
        OutputMax = flow.GetValue<DValue>(OutputMax),
      };
    }
  
    protected override double ComputeElement(Data data, double lhs) {
      double inputPercent = Math.Max(0.0, Math.Min(1.0, (lhs - data.InputMin) / data.InputRange));
      return data.OutputMin * (1.0 - inputPercent) + data.OutputMax * inputPercent;
    }
  }
}
