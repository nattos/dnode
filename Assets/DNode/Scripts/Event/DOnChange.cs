using System;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DOnChange : Unit {
    private const double _epsilon = UnityUtils.DefaultEpsilon;

    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize] public ValueInput Reset;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private bool _hasLatchedValue;
    private DValue _latchedValue;

    protected override void Definition() {
      Input = ValueInput<DEvent>(nameof(Input));
      Reset = ValueInput<bool>(nameof(Reset), false);

      DEvent ComputeFromFlow(Flow flow) {
        DEvent input = DEvent.GetOptionalEventInput(flow, Input);
        if (!_hasLatchedValue || flow.GetValue<bool>(Reset)) {
          _hasLatchedValue = true;
          _latchedValue = input.Value;
          return DEvent.CreateImmediate(_latchedValue, true);
        }
        if (!input.IsTriggered) {
          return DEvent.CreateImmediate(_latchedValue, false);
        }
        DValue newValue = input.Value;
        bool changed;
        if (newValue.Rows != _latchedValue.Rows || newValue.Columns != _latchedValue.Columns) {
          changed = true;
        } else {
          int rows = newValue.Rows;
          int columns = newValue.Columns;
          changed = false;
          for (int row = 0; row < rows; ++row) {
            for (int col = 0; col < columns; ++col) {
              if (Math.Abs(newValue[row, col] - _latchedValue[row, col]) > _epsilon) {
                changed = true;
                break;
              }
            }
            if (changed) {
              break;
            }
          }
        }
        if (changed) {
          _latchedValue = newValue;
        }
        return DEvent.CreateImmediate(_latchedValue, changed);
      }
      result = ValueOutput<DEvent>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
