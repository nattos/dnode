using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DDelayTrigger : Unit {
    [DoNotSerialize] public ValueInput Trigger;
    [DoNotSerialize] public ValueInput MultiTrigger;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultEnvelopeTimeMax, 1.0)][LogScale(D3DConstants.DefaultEnvelopeTimeLogScale)][ShortEditor]  public ValueInput Delay;
    [DoNotSerialize] public ValueInput Reset;

    private LinkedList<double> _queuedDelays = new LinkedList<double>();

    private bool _useMultiTrigger = false;
    [Serialize][Inspectable] public bool UseMultiTrigger {
      get {
        return _useMultiTrigger;
      }
      set {
        if (_useMultiTrigger == value) {
          return;
        }
        _useMultiTrigger = value;
        PortsChanged();
      }
    }

    [Inspectable] public bool UseAbsoluteTime;
    private double _lastAbsoluteTime = 0.0;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      if (_useMultiTrigger) {
        MultiTrigger = ValueInput<DValue>(nameof(MultiTrigger));
      } else {
        Trigger = ValueInput<bool>(nameof(Trigger), false);
      }
      Delay = ValueInput<DValue>(nameof(Delay), 1.0);
      Reset = ValueInput<bool>(nameof(Reset), false);

      bool ComputeFromFlow(Flow flow) {
        if (flow.GetValue<bool>(Reset)) {
          _queuedDelays.Clear();
        }
        bool triggered = flow.GetValue<bool>(Trigger);
        if (triggered) {
          _queuedDelays.AddLast(flow.GetValue<DValue>(Delay));
        }

        double deltaTime;
        if (UseAbsoluteTime) {
          double currentTime = DScriptMachine.CurrentInstance.Transport.AbsoluteTime;
          if (_lastAbsoluteTime == 0.0) {
            deltaTime = DScriptMachine.CurrentInstance.Transport.DeltaTime;
          } else {
            deltaTime = currentTime - _lastAbsoluteTime;
          }
          _lastAbsoluteTime = currentTime;
        } else {
          deltaTime = DScriptMachine.CurrentInstance.Transport.DeltaTime;
        }

        bool delayTriggered = false;
        LinkedListNode<double> node = _queuedDelays.First;
        while (node != null) {
          LinkedListNode<double> nextNode = node.Next;
          node.Value -= deltaTime;
          if (node.Value <= 0) {
            delayTriggered = true;
            _queuedDelays.Remove(node);
          }
          node = nextNode;
        }
        return delayTriggered;
      }
      result = ValueOutput<bool>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
