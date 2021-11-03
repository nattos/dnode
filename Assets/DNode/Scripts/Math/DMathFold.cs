using System;
using Unity.VisualScripting;

namespace DNode {
  public class DMathFold : DUnaryDataScalarOperationBase<DMathFold.Data> {
    public enum FoldShape {
      None,
      Clamp,
      Atan,
      Wrap,
      Triangle,
      Sin,
    }

    public struct Data {
      public FoldShape Shape;
      public double InputMin;
      public double InputMax;
      public double InputRange;
      public double OutputMin;
      public double OutputMax;
    }

    [DoNotSerialize] public ValueInput Shape;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput InputMin;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput InputMax;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput OutputMin;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput OutputMax;

    private bool _outputRangeSameAsInput = true;
    [Serialize][Inspectable] public bool OutputRangeSameAsInput {
      get => _outputRangeSameAsInput;
      set {
        _outputRangeSameAsInput = value;
        PortsChanged();
      }
    }


    protected override void Definition() {
      base.Definition();

      Shape = ValueInput<FoldShape>(nameof(Shape), FoldShape.Triangle);
      InputMin = ValueInput<DValue>(nameof(InputMin), 0.0);
      InputMax = ValueInput<DValue>(nameof(InputMax), 1.0);
      if (!_outputRangeSameAsInput) {
        OutputMin = ValueInput<DValue>(nameof(OutputMin), 0.0);
        OutputMax = ValueInput<DValue>(nameof(OutputMax), 1.0);
      }
    }

    protected override Data GetData(Flow flow, DValue input) {
      double inputMin = flow.GetValue<DValue>(InputMin);
      double inputMax = flow.GetValue<DValue>(InputMax);
      double inputRawRange = inputMax - inputMin;
      double inputRange = Math.Abs(inputRawRange) < UnityUtils.DefaultEpsilon ? 1.0 : inputRawRange;
      bool outputRangeSameAsInput = _outputRangeSameAsInput;
      return new Data {
        Shape = flow.GetValue<FoldShape>(Shape),
        InputMin = inputMin,
        InputMax = inputMax,
        InputRange = inputRange,
        OutputMin = outputRangeSameAsInput ? inputMin : (double)flow.GetValue<DValue>(OutputMin),
        OutputMax = outputRangeSameAsInput ? inputMax : (double)flow.GetValue<DValue>(OutputMax),
      };
    }
  
    protected override double ComputeElement(Data data, double lhs) {
      double inputPercent = (lhs - data.InputMin) / data.InputRange;
      double outputPercent = Fold(data.Shape, inputPercent);
      return data.OutputMin * (1.0 - outputPercent) + data.OutputMax * outputPercent;
    }

    public static double Fold(FoldShape shape, double input) {
      switch (shape) {
        case FoldShape.None:
          return input;
        default:
        case FoldShape.Clamp:
          return Math.Max(0, Math.Min(1.0, input));
        case FoldShape.Atan:
          return Math.Atan(input * 2.0 - 1.0) * 0.5 + 0.5;
        case FoldShape.Wrap:
          return input - Math.Floor(input);
        case FoldShape.Triangle: {
          double semiWrapped = input * 0.5 - Math.Floor(input * 0.5);
          return Math.Abs(semiWrapped - 0.5) * -2.0 + 1.0;
        }
        case FoldShape.Sin:
          return Math.Sin((input - 0.5) * Math.PI) * 0.5 + 0.5;
      }
    }
  }
}
