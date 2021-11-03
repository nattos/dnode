using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace DNode {
  public class DEventOnThreshold : Unit {
    [DoNotSerialize][PortLabelHidden][NoEditor] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden] public ValueInput Threshold;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Input = ValueInput<DValue>(nameof(Input), 0.0);
      Threshold = ValueInput<DValue>(nameof(Threshold), 0.5);

      bool ComputeFromFlow(Flow flow) {
        DValue input = flow.GetValue<DValue>(Input);
        DValue threshold = flow.GetValue<DValue>(Threshold);

        int rows = Math.Max(input.Rows, threshold.Rows);
        int columns = Math.Max(input.Rows, threshold.Rows);
        bool triggered = false;
        for (int row = 0; row < rows; ++row) {
          for (int col = 0; col < columns; ++col) {
            if (input[row, col] > threshold[row, col]) {
              triggered = true;
              break;
            }
          }
          if (triggered) {
            break;
          }
        }
        return triggered;
      }
      result = ValueOutput<bool>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
