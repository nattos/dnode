using Unity.VisualScripting;

namespace DNode {
  public class DIOOscInput : Unit {
    [DoNotSerialize] public ValueInput Address;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private double _currentValue = 0.0;

    protected override void Definition() {
      Address = ValueInput<string>("Address", "/Path");

      DEvent ComputeFromFlow(Flow flow) {
        string address = flow.GetValue<string>(Address);
        bool triggered = DScriptMachine.CurrentInstance.OscManager.TryGetChangedValue(address, out double newValue);
        if (triggered) {
          _currentValue = newValue;
        }
        return DEvent.CreateImmediate(_currentValue, triggered);
      }
      result = ValueOutput<DEvent>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
