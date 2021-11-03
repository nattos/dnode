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
    [DoNotSerialize] public ValueInput Reset;

    [Inspectable] public LatchMode Latch = LatchMode.Trigger;
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
      Trigger = ValueInput<bool>(nameof(Trigger), false);
      Reset = ValueInput<bool>(nameof(Reset), false);

      DEvent ComputeFromFlow(Flow flow) {
        if (!_hasLatchedValue || flow.GetValue<bool>(Reset)) {
          if (!Initial.connections.Any()) {
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
              bool trigger = flow.GetValue<bool>(Trigger);
              if (trigger) {
                DEvent input = DEvent.GetOptionalEventInput(flow, Input);
                _latchedValue = input.Value;
                return DEvent.CreateImmediate(_latchedValue, true);
              } else {
                return DEvent.CreateImmediate(_latchedValue, !OutputIsEventFlow);
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
