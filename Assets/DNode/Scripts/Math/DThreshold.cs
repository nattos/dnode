using Unity.VisualScripting;

namespace DNode {
  public class DThreshold : DTernaryOperationBase<DThreshold.Data> {
    public enum Operation {
      Above,
      AboveOrEqual,
      Below,
      BelowOrEqual,
    }

    public struct Data {
      public Operation Op;
    }

    [DoNotSerialize][PortLabelHidden] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden] public ValueInput Threshold;
    [DoNotSerialize][PortLabelHidden] public ValueInput Replacement;
    [DoNotSerialize] public ValueInput Op;

    protected override string LhsName => "Input";
    protected override string RhsName => "Threshold";
    protected override string AuxName => "Replacement";
    protected override ValueInput LhsPort { set => Input = value; }
    protected override ValueInput RhsPort { set => Threshold = value; }
    protected override ValueInput AuxPort { set => Replacement = value; }

    protected override void Definition() {
      base.Definition();
      Op = ValueInput<Operation>("Op", Operation.AboveOrEqual);
    }

    protected override Data GetData(Flow flow, DValue inputs, DValue thresholds, DValue replacements) {
      return new Data {
        Op = flow.GetValue<Operation>(Op),
      };
    }
  
    protected override double ComputeElement(Data data, double input, double threshold, double replacement) {
      switch (data.Op) {
        case Operation.Above: return input > threshold ? input : replacement;
        case Operation.AboveOrEqual: return input >= threshold ? input : replacement;
        case Operation.Below: return input < threshold ? input : replacement;
        case Operation.BelowOrEqual: return input <= threshold ? input : replacement;
      }
      return input;
    }
  }
}
