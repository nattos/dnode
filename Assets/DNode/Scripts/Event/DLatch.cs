using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DLatch : Unit {
    private static System.Random _random = new System.Random();

    public enum LatchMode {
      Input,
      Trigger,
      Both,
    }

    [DoNotSerialize] public ValueInput Initial;
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize] public ValueInput Trigger;
    [DoNotSerialize] public ValueInput MultiTrigger;
    [DoNotSerialize] public ValueInput Reset;

    [Inspectable] public LatchMode Latch = LatchMode.Trigger;
    
    private bool _useMultiTrigger = false;
    [Serialize][Inspectable] public bool UseMultiTrigger {
      get {
        return _useMultiTrigger;
      }
      set {
        if (_useMultiTrigger == value) {
          return;
        }
        _useMultiTrigger = value;
        PortsChanged();
      }
    }

    [Inspectable] public bool OutputIsEventFlow = true;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    [DoNotSerialize] public ValueOutput resultPrev;

    private bool _hasLatchedValue;
    private int _currentFrameNumber = 0;
    private DValue _latchedValue;

    protected override void Definition() {
      Initial = ValueInput<DValue>(nameof(Initial));
      Input = ValueInput<DEvent>(nameof(Input));
      if (_useMultiTrigger) {
        MultiTrigger = ValueInput<DValue>(nameof(MultiTrigger));
      } else {
        Trigger = ValueInput<bool>(nameof(Trigger), false);
      }
      Reset = ValueInput<bool>(nameof(Reset), false);

      DEvent ComputeFromFlow(Flow flow) {
        if (!_hasLatchedValue || flow.GetValue<bool>(Reset)) {
          if (!Initial.hasAnyConnection) {
            return DEvent.CreateImmediate(_latchedValue, triggered: !OutputIsEventFlow);
          }
          _latchedValue = flow.GetValue<DValue>(Initial);
          _hasLatchedValue = true;
          return DEvent.CreateImmediate(_latchedValue, true);
        }
        int frameNumber = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
        if (_currentFrameNumber != frameNumber) {
          _currentFrameNumber = frameNumber;
          LatchMode latchMode = Latch;
          switch (latchMode) {
            case LatchMode.Input: {
              DEvent input = DEvent.GetOptionalEventInput(flow, Input);
              bool triggered = input.IsTriggered;
              if (triggered) {
                _latchedValue = input.Value;
                return DEvent.CreateImmediate(_latchedValue, true);
              } else {
                return DEvent.CreateImmediate(_latchedValue, !OutputIsEventFlow);
              }
            }
            case LatchMode.Trigger: {
              if (_useMultiTrigger) {
                if (!MultiTrigger.hasAnyConnection) {
                  return DEvent.CreateImmediate(_latchedValue, !OutputIsEventFlow);
                }
                DValue trigger = flow.GetValue<DValue>(MultiTrigger);
                bool anyTriggered = false;
                for (int i = 0; i < trigger.Rows; ++i) {
                  if (trigger.BoolFromRow(i)) {
                    anyTriggered = true;
                    break;
                  }
                }
                if (anyTriggered) {
                  DValue oldValue = _latchedValue;
                  DValue input = DEvent.GetOptionalEventInput(flow, Input).Value;
                  int rows = trigger.Rows;
                  int cols = System.Math.Max(oldValue.Columns, input.Columns);
                  DMutableValue newValue = new DMutableValue(rows, cols);
                  for (int i = 0; i < rows; ++i) {
                    if (trigger.BoolFromRow(i)) {
                      newValue.SetRow(i, input, i);
                    } else {
                      newValue.SetRow(i, oldValue, i);
                    }
                  }
                  _latchedValue = newValue.ToValue();
                  return DEvent.CreateImmediate(_latchedValue, true);
                }
                return DEvent.CreateImmediate(_latchedValue, !OutputIsEventFlow);
              } else {
                bool trigger = flow.GetValue<bool>(Trigger);
                if (trigger) {
                  DEvent input = DEvent.GetOptionalEventInput(flow, Input);
                  _latchedValue = input.Value;
                  return DEvent.CreateImmediate(_latchedValue, true);
                } else {
                  return DEvent.CreateImmediate(_latchedValue, !OutputIsEventFlow);
                }
              }
            }
            default:
            case LatchMode.Both: {
              bool trigger = flow.GetValue<bool>(Trigger);
              if (trigger) {
                DEvent input = DEvent.GetOptionalEventInput(flow, Input);
                _latchedValue = input.Value;
                return DEvent.CreateImmediate(_latchedValue, true);
              } else {
                return DEvent.CreateImmediate(_latchedValue, !OutputIsEventFlow);
              }
            }
          }
        } else {
          return _latchedValue;
        }
      }
      result = ValueOutput<DEvent>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
      resultPrev = ValueOutput<DValue>("Prev", flow => _latchedValue);
    }
  }
}
