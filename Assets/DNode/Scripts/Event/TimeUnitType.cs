using System;

namespace DNode {
  public enum TimeUnitType {
    Seconds,
    Bar,
    Beat,
    Looped,
  }

  public static class TimeUnitTypeExtensions {
    public static double GetTime(this TimeUnitType type, double multiplier = 1.0, bool sync = false) {
      double timeNumber = GetTimeNumber(type) * multiplier;
      if (sync) {
        timeNumber -= Math.Floor(timeNumber);
      }
      return timeNumber;
    }

    private static double GetTimeNumber(TimeUnitType type) {
      Transport transport = DScriptMachine.CurrentInstance.Transport;
      switch (type) {
        case TimeUnitType.Seconds:
          return transport.Time;
        case TimeUnitType.Bar:
          return transport.Bar;
        default:
        case TimeUnitType.Beat:
          return transport.Beat;
        case TimeUnitType.Looped:
          return transport.LoopNumber;
      }
    }
  }
}
