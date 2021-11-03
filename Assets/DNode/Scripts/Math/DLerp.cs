using System;
using Unity.VisualScripting;

namespace DNode {
  public class DLerp : DTernaryOperationBase<DLerp.Data> {
    public struct Data {
      public bool Clamp;
    }

    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput T;
    [DoNotSerialize] public ValueInput Clamp;

    protected override string LhsName => "A";
    protected override string RhsName => "B";
    protected override string AuxName => "T";
    protected override ValueInput AuxPort { set => T = value; }

    protected override void Definition() {
      base.Definition();
      Clamp = ValueInput<bool>("Clamp", true);
    }

    protected override Data GetData(Flow flow, DValue inputs, DValue thresholds, DValue replacements) {
      return new Data {
        Clamp = flow.GetValue<bool>(Clamp),
      };
    }
  
    protected override double ComputeElement(Data data, double a, double b, double t) {
      if (data.Clamp) {
        t = Math.Max(0.0, Math.Min(1.0, t));
      }
      return UnityUtils.Lerp(a, b, t);
    }
  }
}
