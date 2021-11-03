using Unity.VisualScripting;

namespace DNode {
  public class DLaunchableTriggerValue {
    [DoNotSerialize] public IDLaunchable Target;

    public bool Trigger;

    public static implicit operator DLaunchableTriggerValue(bool value) {
      return new DLaunchableTriggerValue { Trigger = value };
    }

    public static implicit operator bool(DLaunchableTriggerValue value) {
      return value.Trigger;
    }

    public static DLaunchableTriggerValue FromUnit(IDLaunchable target) {
      return new DLaunchableTriggerValue { Target = target };
    }

    static DLaunchableTriggerValue() {
      DValue.Init();
    }
  }
}
