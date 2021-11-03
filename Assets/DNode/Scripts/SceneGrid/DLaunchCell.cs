using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DLaunchCell : DFrameUnit, IDLaunchable {
    [DoNotSerialize] public ValueInput PreviousSiblingInput;
    [DoNotSerialize] public ValueInput Options;
    [DoNotSerialize] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden][PortKey("CustomTrigger")] public ValueInput CustomTriggerInput;
    [DoNotSerialize][PortLabelHidden] public ValueOutput result;

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

    [DoNotSerialize] public DLaunchOptions LaunchOptions;
    [DoNotSerialize] public bool HasInput { get; set; }
    [DoNotSerialize] public DLaunchCell PreviousSibling;

    [DoNotSerialize] public DLauncher LayoutGrid;
    [DoNotSerialize] public int LayoutColumn;
    [DoNotSerialize] public int LayoutScene;

    [DoNotSerialize] public bool Triggered { get; set; }
    [DoNotSerialize] public bool StatusPlaying { get; set; }
    [DoNotSerialize] public bool StatusQueued { get; set; }
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
        LaunchOptions = DNodeUtils.GetOptional<DLaunchOptions>(flow, Options, LaunchOptions);
        HasInput = Input.connections.Any();

        PreviousSibling = DNodeUtils.GetOptional<DLaunchCell>(flow, PreviousSiblingInput);
        return this;
      }));
    }
  }
}
