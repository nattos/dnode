using Unity.VisualScripting;

namespace DNode {
  public class DLaunchHeader : DFrameUnit, IDLaunchable {
    [DoNotSerialize] public ValueInput NameInput;
    [DoNotSerialize] public ValueInput PreviousHeaderInput;
    [DoNotSerialize][PortLabelHidden][PortKey("CustomTrigger")] public ValueInput CustomTriggerInput;
    [DoNotSerialize][PortLabelHidden] public ValueOutput result;

    [DoNotSerialize] public string Name;
    [DoNotSerialize] public DLaunchHeader PreviousHeader;

    [DoNotSerialize] public bool HasInput => false;
    [DoNotSerialize] public bool Triggered { get; set; }
    [DoNotSerialize] public bool StatusPlaying { get; set; }
    [DoNotSerialize] public bool StatusQueued { get; set; }
    [DoNotSerialize] public double StatusPlayingQuantizationPercent { get; set; }
    [DoNotSerialize] public double StatusQueuedQuantizationPercent { get; set; }
    [DoNotSerialize] public int StatusLaunchedOnFrameNumber { get; set; } = -1;
    string IDLaunchable.LaunchLabelOverride => "Stop";
    DLaunchQuantization IDLaunchable.LaunchOptionQuantization => DLaunchQuantization.Global;

    [DoNotSerialize] public int LayoutColumn;

    protected override void Definition() {
      base.Definition();

      NameInput = ValueInput<string>("Name", "# Column");
      PreviousHeaderInput = ValueInput<DLaunchHeader>("Prev");
      CustomTriggerInput = ValueInput("CustomTrigger", DLaunchableTriggerValue.FromUnit(this));

      result = ValueOutput<DLaunchHeader>("result", DNodeUtils.CachePerFrame(flow => {
        flow.GetValue<DLaunchableTriggerValue>(CustomTriggerInput).Target = this;
        Name = flow.GetValue<string>(NameInput);
        PreviousHeader = DNodeUtils.GetOptional<DLaunchHeader>(flow, PreviousHeaderInput);
        return this;
      }));
    }
  }
}
