using System;
using Unity.VisualScripting;

namespace DNode {
  public class DSetTransport : Unit {
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1000, 0)] public ValueInput DriveTimeBeats;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(1, 1000, 120)] public ValueInput SetTempo;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(1, 16, 4)][LogScale] public ValueInput SetBeatsPerBar;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(1.0 / 64, 32, 1)][LogScale] public ValueInput SetLoopLengthBars;

    private DEnvironmentOverrideProviderHandle _environmentOverrideProvider;

    public override void AfterAdd() {
      base.AfterAdd();
      _environmentOverrideProvider = new DEnvironmentOverrideProviderHandle(graph, GetEnvironmentOverrides);
    }

    public override void Dispose() {
      base.Dispose();
      _environmentOverrideProvider.Dispose();
    }

    private DEnvironmentOverrides GetEnvironmentOverrides(Flow flow) {
      return new DEnvironmentOverrides {
        TimeBeats = flow.GetValue<DEvent>(DriveTimeBeats).OptionalValue,
        Tempo = flow.GetValue<DEvent>(SetTempo).OptionalValue,
        BeatsPerBar = flow.GetValue<DEvent>(SetBeatsPerBar).OptionalValue,
        LoopLengthBars = flow.GetValue<DEvent>(SetLoopLengthBars).OptionalValue,
      };
    }

    protected override void Definition() {
      DriveTimeBeats = ValueInput<DEvent>("DriveTimeBeats", default);
      SetTempo = ValueInput<DEvent>("SetTempo", DEvent.CreateImmediate(120, false));
      SetBeatsPerBar = ValueInput<DEvent>("SetBeatsPerBar", DEvent.CreateImmediate(4, false));
      SetLoopLengthBars = ValueInput<DEvent>("SetLoopLengthBars", DEvent.CreateImmediate(1, false));
    }
  }
}
