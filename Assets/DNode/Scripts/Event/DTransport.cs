using System;
using Unity.VisualScripting;

namespace DNode {
  public class DTransport : Unit {
    [Inspectable] public bool IsEventFlow = true;

    [DoNotSerialize] public ValueOutput resultFrameNumber;
    [DoNotSerialize] public ValueOutput resultTime;
    [DoNotSerialize] public ValueOutput resultBeat;
    [DoNotSerialize] public ValueOutput resultBar;
    [DoNotSerialize] public ValueOutput resultLoopTime;
    [DoNotSerialize] public ValueOutput resultLoopPhase;
    [DoNotSerialize] public ValueOutput resultLoopBeat;
    [DoNotSerialize] public ValueOutput resultLooped;

    protected override void Definition() {
      Func<Flow, DEvent> WrapInChangeDetector(Func<Flow, double> func) {
        double currentValue = double.MinValue;
        return DNodeUtils.CachePerFrame(flow => {
          double nextValue = func.Invoke(flow);
          bool triggered = !IsEventFlow || Math.Floor(currentValue) != Math.Floor(nextValue);
          currentValue = nextValue;
          return DEvent.CreateImmediate(nextValue, triggered);
        });
      }

      Func<Flow, bool> WrapInResetDetector(Func<Flow, double> func) {
        double currentValue = double.MinValue;
        return DNodeUtils.CachePerFrame(flow => {
          double nextValue = func.Invoke(flow);
          bool triggered = nextValue < currentValue;
          currentValue = nextValue;
          return triggered;
        });
      }

      resultFrameNumber = ValueOutput<int>("Frame", flow => DScriptMachine.CurrentInstance.Transport.AbsoluteFrame);
      resultTime = ValueOutput<double>("Time", flow => DScriptMachine.CurrentInstance.Transport.Time);
      resultBeat = ValueOutput<DEvent>("Beat", WrapInChangeDetector(flow => DScriptMachine.CurrentInstance.Transport.Beat));
      resultBar = ValueOutput<DEvent>("Bar", WrapInChangeDetector(flow => DScriptMachine.CurrentInstance.Transport.Bar));
      resultLoopTime = ValueOutput<double>("Loop Time", flow => DScriptMachine.CurrentInstance.Transport.LoopTime);
      resultLoopPhase = ValueOutput<double>("Loop Phase", flow => DScriptMachine.CurrentInstance.Transport.LoopPhase);
      resultLoopBeat = ValueOutput<DEvent>("Loop Beat", WrapInChangeDetector(flow => DScriptMachine.CurrentInstance.Transport.LoopBeat));
      resultLooped = ValueOutput<bool>("Looped", WrapInResetDetector(flow => DScriptMachine.CurrentInstance.Transport.LoopPhase));
    }
  }
}
