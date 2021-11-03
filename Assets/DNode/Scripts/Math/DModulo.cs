using Unity.VisualScripting;

namespace DNode {
  public class DModulo : DUnaryDataScalarOperationBase<DModulo.Data> {
    public struct Data {
      public double Denominator;
      public bool WrapNegative;
    }

    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput Denominator;
    [DoNotSerialize] public ValueInput WrapNegative;

    protected override void Definition() {
      base.Definition();

      Denominator = ValueInput<DValue>(nameof(Denominator), 1.0);
      WrapNegative = ValueInput<bool>(nameof(WrapNegative), false);
    }

    protected override Data GetData(Flow flow, DValue input) {
      return new Data {
        Denominator = flow.GetValue<DValue>(Denominator),
        WrapNegative = flow.GetValue<bool>(WrapNegative),
      };
    }

    protected override double ComputeElement(Data data, double lhs) {
      if (data.WrapNegative) {
        if (lhs < 0) {
          return (data.Denominator - ((-lhs) % data.Denominator)) % data.Denominator;
        }
        return lhs % data.Denominator;
      }
      return lhs % data.Denominator;
    }
  }
}
