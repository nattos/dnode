using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class Transport {
    public double Time = 0;
    public double Bar = 0;
    public double Beat = 0;
    public double LoopTime => LoopBeat * TempoBeatsPerSecond;
    public double LoopNumber => Beat / LoopLengthBeats;
    public double LoopBeat => UnityUtils.WrappedModulo(Beat, LoopLengthBeats);
    public double LoopPhase => UnityUtils.WrappedModulo(Beat, LoopLengthBeats) / LoopLengthBeats;
    public int AbsoluteFrame = 0;
    public double DeltaTime => UnityEngine.Time.deltaTime;

    public double Tempo = 120.0;
    public double TempoBeatsPerSecond => Tempo / 60.0;
    public double BeatsPerBar = 4.0;
    public double LoopLengthBars = 1.0;
    public double LoopLengthBeats => LoopLengthBars * BeatsPerBar;

    public void DriveFromTimeBeats(double timeBeats) {
      Beat = timeBeats;
      Bar = Beat / Math.Max(1.0, BeatsPerBar);
      Time = Beat / Math.Max(UnityUtils.DefaultEpsilon, TempoBeatsPerSecond);
    }

    public void DriveFromTimeSeconds(double time) {
      Time = time;
      Beat = time * TempoBeatsPerSecond;
      Bar = Beat / Math.Max(1.0, BeatsPerBar);
    }
  }
}
