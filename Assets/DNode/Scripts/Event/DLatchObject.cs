using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DLatchObject : Unit {
    [DoNotSerialize] public ValueInput Initial;
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize] public ValueInput Trigger;
    [DoNotSerialize] public ValueInput Reset;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    [DoNotSerialize] public ValueOutput resultPrev;

    private bool _hasLatchedValue;
    private int _currentFrameNumber = 0;
    private object _latchedValue;

    protected override void Definition() {
      Initial = ValueInput<object>(nameof(Initial));
      Input = ValueInput<object>(nameof(Input));
      Trigger = ValueInput<bool>(nameof(Trigger), false);
      Reset = ValueInput<bool>(nameof(Reset), false);

      object ComputeFromFlow(Flow flow) {
        if (!_hasLatchedValue || flow.GetValue<bool>(Reset)) {
          if (!Initial.connections.Any()) {
            return _latchedValue;
          }
          _latchedValue = flow.GetValue<object>(Initial);
          _hasLatchedValue = true;
          return _latchedValue;
        }
        int frameNumber = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
        if (_currentFrameNumber != frameNumber) {
          _currentFrameNumber = frameNumber;
          bool trigger = flow.GetValue<bool>(Trigger);
          if (trigger) {
            _latchedValue = flow.GetValue<object>(Input);
          }
        }
        return _latchedValue;
      }
      result = ValueOutput<object>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
      resultPrev = ValueOutput<object>("Prev", flow => _latchedValue);
    }
  }
}
