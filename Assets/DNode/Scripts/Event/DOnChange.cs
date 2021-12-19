using System;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DOnChange : Unit {
    private const double _epsilon = UnityUtils.DefaultEpsilon;

    [DoNotSerialize] public ValueInput Initial;
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize] public ValueInput Reset;

    [Inspectable] public bool UseMultiTrigger = false;
    [Inspectable] public bool TriggerOnReset = true;

    private bool _useExplicitInitialState = false;
    [Serialize][Inspectable] public bool UseExplicitInitialState {
      get => _useExplicitInitialState;
      set {
        if (value == _useExplicitInitialState) {
          return;
        }
        _useExplicitInitialState = value;
        PortsChanged();
      }
    }

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private bool _hasLatchedValue;
    private DValue _latchedValue;

    protected override void Definition() {
      if (UseExplicitInitialState) {
        Initial = ValueInput<DValue>(nameof(Initial));
      }
      Input = ValueInput<DEvent>(nameof(Input));
      Reset = ValueInput<bool>(nameof(Reset), false);

      DEvent ComputeFromFlow(Flow flow) {
        DEvent input = DEvent.GetOptionalEventInput(flow, Input);
        if (!_hasLatchedValue || flow.GetValue<bool>(Reset)) {
          _hasLatchedValue = true;
          if (UseExplicitInitialState && Initial.hasAnyConnection) {
            _latchedValue = flow.GetValue<DValue>(Initial);
          } else {
            _latchedValue = input.Value;
            if (UseMultiTrigger) {
              DMutableValue emptyMultiTriggerResult = new DMutableValue(_latchedValue.Rows, 1);
              if (TriggerOnReset) {
                for (int row = 0; row < emptyMultiTriggerResult.Rows; ++row) {
                  emptyMultiTriggerResult[row, 0] = 1.0;
                }
              }
              return DEvent.CreateImmediate(emptyMultiTriggerResult.ToValue(), TriggerOnReset);
            } else {
              return DEvent.CreateImmediate(_latchedValue, TriggerOnReset);
            }
          }
        }
        DMutableValue newMultiTriggerResult = default;
        if (UseMultiTrigger) {
          newMultiTriggerResult = new DMutableValue(_latchedValue.Rows, 1);
        }
        if (!input.IsTriggered) {
          if (UseMultiTrigger) {
            return DEvent.CreateImmediate(newMultiTriggerResult.ToValue(), true);
          } else {
            return DEvent.CreateImmediate(_latchedValue, false);
          }
        }
        DValue newValue = input.Value;
        bool changed = false;
        bool multiChanged = false;
        if (newValue.Rows != _latchedValue.Rows || newValue.Columns != _latchedValue.Columns) {
          changed = true;
        } else {
          int rows = newValue.Rows;
          int columns = newValue.Columns;
          changed = false;
          for (int row = 0; row < rows; ++row) {
            for (int col = 0; col < columns; ++col) {
              if (Math.Abs(newValue[row, col] - _latchedValue[row, col]) > _epsilon) {
                if (UseMultiTrigger) {
                  newMultiTriggerResult[row, 0] = 1.0;
                  multiChanged = true;
                } else {
                  changed = true;
                }
                break;
              }
            }
            if (changed) {
              break;
            }
          }
        }
        if (changed || multiChanged) {
          _latchedValue = newValue;
        }
        if (UseMultiTrigger) {
          return DEvent.CreateImmediate(newMultiTriggerResult.ToValue(), multiChanged);
        } else {
          return DEvent.CreateImmediate(_latchedValue, changed);
        }
      }
      result = ValueOutput<DEvent>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
