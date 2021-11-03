using System;
using Unity.VisualScripting;

namespace DNode {
  public class DPack : DMultiInputUnit<DValue> {
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      base.Definition();
      foreach (var input in multiInputs) {
        input.SetDefaultValue(default(DValue));
      }

      DValue ComputeFromFlow(Flow flow) {
        int columns = multiInputs.Count;
        int rowCount = 1;
        DValue[] inputValues = new DValue[columns];
        for (int i = 0; i < columns; ++i) {
          DValue input = flow.GetValue<DValue>(multiInputs[i]);
          inputValues[i] = input;
          rowCount = Math.Max(rowCount, input.Rows);
        }
        DMutableValue result = new DMutableValue(rowCount, columns);
        for (int i = 0; i < columns; ++i) {
          DValue input = inputValues[i];
          int inputCol = input.Columns == 1 ? 0 : i;
          for (int row = 0; row < rowCount; ++row) {
            result[row, i] = input[row, inputCol];
          }
        }
        return result.ToValue();
      }
      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
