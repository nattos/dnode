using Unity.VisualScripting;

namespace DNode {
  public class DIOOscOutput : DIOOutputNode {
    [DoNotSerialize][PortLabelHidden][Scalar] public ValueInput Input;
    [DoNotSerialize] public ValueInput Address;

    private double _currentValue = 0.0;

    protected override void Definition() {
      Input = ValueInput<DEvent>("Input", default);
      Address = ValueInput<string>("Address", "/Path");
    }

    public override void ComputeFromFlow(Flow flow) {
      DEvent input = flow.GetValue<DEvent>(Input);
      if (input.IsTriggered) {
        _currentValue = input.Value;
        string address = flow.GetValue<string>(Address);
        DScriptMachine.CurrentInstance.OscManager.SendValueChange(address, _currentValue);
      }
    }
  }
}
