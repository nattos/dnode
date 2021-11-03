using System;
using Unity.VisualScripting;

namespace DNode {
  public class DUnpack : Unit {
    [DoNotSerialize][PortLabelHidden][NoEditor] public ValueInput Input;

    [DoNotSerialize] public ValueOutput resultX;
    [DoNotSerialize] public ValueOutput resultY;
    [DoNotSerialize] public ValueOutput resultZ;
    [DoNotSerialize] public ValueOutput resultW;

    protected override void Definition() {
      Input = ValueInput<DValue>("Input", 0);
      resultX = ValueOutput<DValue>("X", GetColumnFunc(0));
      resultY = ValueOutput<DValue>("Y", GetColumnFunc(1));
      resultZ = ValueOutput<DValue>("Z", GetColumnFunc(2));
      resultW = ValueOutput<DValue>("W", GetColumnFunc(3));
    }

    private Func<Flow, DValue> GetColumnFunc(int column) {
      return flow => {
        DValue input = flow.GetValue<DValue>(Input);
        int rows = input.Rows;
        DMutableValue result = new DMutableValue(rows, 1);
        for (int row = 0; row < rows; ++row) {
          result[row, 0] = input[row, column];
        }
        return result.ToValue();
      };
    }
  }
}
