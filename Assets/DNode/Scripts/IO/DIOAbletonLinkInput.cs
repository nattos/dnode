using System;
using Unity.VisualScripting;

namespace DNode {
  public class DIOAbletonLinkInput : Unit {
    [DoNotSerialize] public ValueOutput resultTimeBeats;
    [DoNotSerialize] public ValueOutput resultTempo;
    [DoNotSerialize] public ValueOutput resultNumPeers;

    protected override void Definition() {
      (double beat, double tempo, int numPeers) ComputeFromFlow(Flow flow) {
        AbletonLink link = AbletonLink.Instance;
        var transport = DScriptMachine.CurrentInstance.Transport;
        double quantum = transport.BeatsPerBar * transport.LoopLengthBars;
        if (Math.Abs(link.quantum() - quantum) > UnityUtils.DefaultEpsilon) {
          link.setQuantum(quantum);
        }
        link.update(out double beat, out double _, out double tempo, out double _, out int numPeers);
        return (beat, tempo, numPeers);
      }
      var resultFunc = DNodeUtils.CachePerFrame(ComputeFromFlow);
      resultTimeBeats = ValueOutput<double>("TimeBeats", flow => resultFunc(flow).beat);
      resultTempo = ValueOutput<double>("Tempo", flow => resultFunc(flow).tempo);
      resultNumPeers = ValueOutput<int>("NumPeers", flow => resultFunc(flow).numPeers);
    }
  }
}
