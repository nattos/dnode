using System;
using Unity.VisualScripting;

namespace DNode {
  public class DTick : Unit {
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 32, 1)][LogScale][ShortEditor] public ValueInput Length;
    [DoNotSerialize] public ValueInput Unit;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private int _currentFrameNumber = 0;
    private double _currentTimeValue = 0;
    private bool _currentTriggered = false;

    protected override void Definition() {
      Length = ValueInput<DValue>("Length", 1);
      Unit = ValueInput<TimeUnitType>("Unit", TimeUnitType.Beat);

      bool ComputeFromFlow(Flow flow) {
        Transport transport = DScriptMachine.CurrentInstance.Transport;
        if (transport.AbsoluteFrame != _currentFrameNumber) {
          _currentFrameNumber = transport.AbsoluteFrame;
          double nextTimeValue = flow.GetValue<TimeUnitType>(Unit).GetTime();
          nextTimeValue /= Math.Max(UnityUtils.DefaultEpsilon, flow.GetValue<DValue>(Length));
          int previousCoarse = (int)Math.Floor(_currentTimeValue);
          int nextCoarse = (int)Math.Floor(nextTimeValue);
          _currentTriggered = nextCoarse != previousCoarse;
          _currentTimeValue = nextTimeValue;
        }
        return _currentTriggered;
      }
      result = ValueOutput<bool>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
