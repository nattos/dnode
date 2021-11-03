using Unity.VisualScripting;

namespace DNode {
  public class DRandomEvent : Unit {
    private static System.Random _random = new System.Random();

    [DoNotSerialize] public ValueInput Rows;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultRateMax, 1)][LogScale(D3DConstants.DefaultRateLogScale)][ShortEditor] public ValueInput PerSecond;
    [DoNotSerialize] public ValueInput Enabled;
    [DoNotSerialize][PortLabelHidden][Vector4] public ValueInput Value;

    private bool _useMultiRow = false;
    [Serialize] [Inspectable] public bool UseMultiRow {
      get => _useMultiRow;
      set {
        _useMultiRow = value;
        PortsChanged();
      }
    }

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      if (_useMultiRow) {
        Rows = ValueInput<int>("Rows", 1);
      }
      PerSecond = ValueInput<DValue>("PerSecond", 1.0);
      Enabled = ValueInput<bool>("Enabled", true);
      if (!_useMultiRow) {
        Value = ValueInput<DValue>("Value", 1.0);
      }

      if (_useMultiRow) {
        DValue ComputeFromFlow(Flow flow) {
          int rows = flow.GetValue<int>(Rows);
          DMutableValue result = new DMutableValue(rows, 1);
          bool enabled = flow.GetValue<bool>(Enabled);
          if (enabled) {
            for (int i = 0; i < rows; ++i) {
              double eventsPerSecond = flow.GetValue<DValue>(PerSecond);
              double dt = DScriptMachine.CurrentInstance.Transport.DeltaTime;
              double eventsPerDt = dt * eventsPerSecond;
              double p = _random.NextDouble();
              bool triggered = p < eventsPerDt;
              result[i, 0] = triggered ? 1.0 : 0.0;
            }
          }
          return result.ToValue();
        }
        result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
      } else {
        DEvent ComputeFromFlow(Flow flow) {
          bool enabled = flow.GetValue<bool>(Enabled);
          bool triggered = false;
          if (enabled) {
            double eventsPerSecond = flow.GetValue<DValue>(PerSecond);
            double dt = DScriptMachine.CurrentInstance.Transport.DeltaTime;
            double eventsPerDt = dt * eventsPerSecond;
            double p = _random.NextDouble();
            triggered = p < eventsPerDt;
          }
          return DEvent.Create(() => flow.GetValue<DValue>(Value), triggered);
        }
        result = ValueOutput<DEvent>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
      }
    }
  }
}
