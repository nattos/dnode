using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DLaunchCell : DFrameUnit, IDLaunchable {
    [DoNotSerialize] public ValueInput PreviousSiblingInput;
    [DoNotSerialize] public ValueInput Options;
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden][PortKey("CustomTrigger")] public ValueInput CustomTriggerInput;
    [DoNotSerialize][PortLabelHidden] public ValueOutput result;
    [DoNotSerialize][PortLabelHidden] public ValueOutput resultPlaying;
    [DoNotSerialize][PortLabelHidden] public ValueOutput resultTriggered;

    private bool _useExternalOptions = false;
    [Inspectable] public bool UseExternalOptions {
      get => _useExternalOptions;
      set {
        _useExternalOptions = value;
        PortsChanged();
      }
    }

    [Serialize][Inspectable] public DLaunchQuantization Quantization { get => LaunchOptions.Quantization; set => LaunchOptions.Quantization = value; }
    [Serialize][Inspectable] public double LengthBeats { get => LaunchOptions.LengthBeats; set => LaunchOptions.LengthBeats = value; }
    [Serialize][Inspectable] public DLaunchFollowActionType FollowActionA { get => LaunchOptions.FollowActionA; set => LaunchOptions.FollowActionA = value; }
    [Serialize][Inspectable] public DLaunchFollowActionType FollowActionB { get => LaunchOptions.FollowActionB; set => LaunchOptions.FollowActionB = value; }
    [Serialize][Inspectable][InspectorRange(0, 1)] public double FollowABChance { get => LaunchOptions.FollowABChance; set => LaunchOptions.FollowABChance = value; }
    [Serialize][Inspectable] public bool FireTriggerOnLoop { get => LaunchOptions.FireTriggerOnLoop; set => LaunchOptions.FireTriggerOnLoop = value; }
    [Serialize][Inspectable] public bool IsDefaultCell = false;

    [DoNotSerialize] public DLaunchOptions LaunchOptions;
    [DoNotSerialize] public bool HasInput => Input.hasAnyConnection;
    [DoNotSerialize] public DLaunchCell PreviousSibling;

    [DoNotSerialize] public DLauncher LayoutGrid;
    [DoNotSerialize] public int LayoutColumn;
    [DoNotSerialize] public int LayoutScene;

    [DoNotSerialize] public bool Triggered { get; set; }
    [DoNotSerialize] public bool StatusPlaying { get; set; }
    [DoNotSerialize] public bool StatusQueued { get; set; }
    [DoNotSerialize] public double StatusPlayingQuantizationPercent { get; set; }
    [DoNotSerialize] public double StatusQueuedQuantizationPercent { get; set; }
    [DoNotSerialize] public int StatusLaunchedOnFrameNumber { get; set; } = -1;
    string IDLaunchable.LaunchLabelOverride => null;
    DLaunchQuantization IDLaunchable.LaunchOptionQuantization => LaunchOptions.Quantization;

    protected override void Definition() {
      base.Definition();

      PreviousSiblingInput = ValueInput<DLaunchCell>("Prev");
      if (_useExternalOptions) {
        Options = ValueInput<DLaunchOptions>("Options");
      }
      Input = ValueInput<DFrameNodes>("Input");
      CustomTriggerInput = ValueInput("CustomTrigger", DLaunchableTriggerValue.FromUnit(this));

      result = ValueOutput<DLaunchCell>("result", DNodeUtils.CachePerFrame(flow => {
        flow.GetValue<DLaunchableTriggerValue>(CustomTriggerInput).Target = this;
        LaunchOptions = _useExternalOptions ? DNodeUtils.GetOptional<DLaunchOptions>(flow, Options, LaunchOptions) : LaunchOptions;

        PreviousSibling = DNodeUtils.GetOptional<DLaunchCell>(flow, PreviousSiblingInput);
        return this;
      }));
      resultPlaying = ValueOutput<bool>("resultPlaying", flow => StatusPlaying);
      resultTriggered = ValueOutput<bool>("resultTriggered", flow => DScriptMachine.CurrentInstance.Transport.AbsoluteFrame == StatusLaunchedOnFrameNumber);
    }
  }
}
