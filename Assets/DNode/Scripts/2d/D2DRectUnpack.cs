using Unity.VisualScripting;

namespace DNode {
  public class D2DRectUnpack : Unit {
    [DoNotSerialize][PortLabelHidden][Vector4][ScreenRectMinMaxRange] public ValueInput RectMinMax;

    [DoNotSerialize] public ValueOutput resultMin;
    [DoNotSerialize] public ValueOutput resultMax;
    [DoNotSerialize] public ValueOutput resultCenter;

    protected override void Definition() {
      RectMinMax = ValueInput<DValue>(nameof(RectMinMax), default);

      resultMin = ValueOutput<DValue>("Min", DNodeUtils.CachePerFrame(flow => {
        DValue minMax = flow.GetValue<DValue>(RectMinMax);
        int rows = minMax.Rows;
        int cols = 2;
        DMutableValue result = new DMutableValue(rows, cols);
        for (int row = 0; row < rows; ++row) {
          result[row, 0] = minMax[row, 0];
          result[row, 1] = minMax[row, 1];
        }
        return result.ToValue();
      }));
      resultMin = ValueOutput<DValue>("Max", DNodeUtils.CachePerFrame(flow => {
        DValue minMax = flow.GetValue<DValue>(RectMinMax);
        int rows = minMax.Rows;
        int cols = 2;
        DMutableValue result = new DMutableValue(rows, cols);
        for (int row = 0; row < rows; ++row) {
          result[row, 0] = minMax[row, 2];
          result[row, 1] = minMax[row, 3];
        }
        return result.ToValue();
      }));
      resultCenter = ValueOutput<DValue>("Center", DNodeUtils.CachePerFrame(flow => {
        DValue minMax = flow.GetValue<DValue>(RectMinMax);
        int rows = minMax.Rows;
        int cols = 2;
        DMutableValue result = new DMutableValue(rows, cols);
        for (int row = 0; row < rows; ++row) {
          result[row, 0] = (minMax[row, 0] + minMax[row, 2]) * 0.5;
          result[row, 1] = (minMax[row, 1] + minMax[row, 3]) * 0.5;
        }
        return result.ToValue();
      }));
    }
  }
}
