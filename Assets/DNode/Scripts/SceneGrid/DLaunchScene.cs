using Unity.VisualScripting;

namespace DNode {
  public class DLaunchScene : DFrameUnit, IDLaunchable {
    [DoNotSerialize] public ValueInput NameInput;
    [DoNotSerialize] public ValueInput FirstCellInput;
    [DoNotSerialize] public ValueInput NextSceneInput;
    [DoNotSerialize][PortLabelHidden][PortKey("CustomTrigger")] public ValueInput CustomTriggerInput;
    [DoNotSerialize][PortLabelHidden] public ValueOutput result;

    [DoNotSerialize] public string Name;
    [DoNotSerialize] public DLaunchCell FirstCell;
    [DoNotSerialize] public DLaunchScene NextScene;

    [DoNotSerialize] public int LayoutSceneNumber;
    [DoNotSerialize] public DLauncher LayoutGrid;

    [DoNotSerialize] public bool HasInput => true;
    [DoNotSerialize] public bool Triggered { get; set; }
    [DoNotSerialize] public bool StatusPlaying { get; set; }
    [DoNotSerialize] public bool StatusQueued { get; set; }
    string IDLaunchable.LaunchLabelOverride => null;
    DLaunchQuantization IDLaunchable.LaunchOptionQuantization => DLaunchQuantization.Global;

    protected override void Definition() {
      base.Definition();

      NameInput = ValueInput<string>("Name", "# Scene");
      FirstCellInput = ValueInput<DLaunchCell>("Cell");
      NextSceneInput = ValueInput<DLaunchScene>("NextScene");
      CustomTriggerInput = ValueInput("CustomTrigger", DLaunchableTriggerValue.FromUnit(this));

      result = ValueOutput<DLaunchScene>("result", DNodeUtils.CachePerFrame(flow => {
        flow.GetValue<DLaunchableTriggerValue>(CustomTriggerInput).Target = this;
        Name = flow.GetValue<string>(NameInput);
        FirstCell = DNodeUtils.GetOptional<DLaunchCell>(flow, FirstCellInput);
        NextScene = DNodeUtils.GetOptional<DLaunchScene>(flow, NextSceneInput);
        return this;
      }));
    }
  }
}
