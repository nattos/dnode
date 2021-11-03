using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace DNode {
  public class DConcat : DMultiInputUnit<DValue> {
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      base.Definition();

      DValue ComputeFromFlow(Flow flow) {
        int rowCount = 0;
        int columnCount = 0;
        List<DValue> values = new List<DValue>();
        foreach (ValueInput input in multiInputs) {
          DValue value = flow.GetValue<DValue>(input);
          values.Add(value);
          rowCount += value.Rows;
          columnCount = Math.Max(columnCount, value.Columns);
        }

        double[] resultArray = new double[rowCount * columnCount];
        DMutableValue result = new DMutableValue { ValueArray = resultArray, Columns = columnCount, Rows = rowCount };
        int row = 0;
        foreach (DValue value in values) {
          for (int i = 0; i < value.Rows; ++i) {
            result.SetRow(row++, value, i);
          }
        }

        return result.ToValue();
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
