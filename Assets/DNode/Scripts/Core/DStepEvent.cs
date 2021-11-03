using Unity.VisualScripting;

namespace DNode {
  public class DStepEvent : EventUnit<DEmptyData> {
    public const string TriggerName = nameof(DStepEvent) + ".Trigger";
    protected override bool register => true;

    public override EventHook GetHook(GraphReference reference) {
      return new EventHook(TriggerName, reference.gameObject);
    }

    protected override void AssignArguments(Flow flow, DEmptyData data) {}

    public static void Trigger(DScriptMachine component) {
      EventBus.Trigger<DEmptyData>(new EventHook(TriggerName, component.gameObject), default);
    }
  }
}
