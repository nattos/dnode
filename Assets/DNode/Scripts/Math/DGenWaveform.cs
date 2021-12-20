using System;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DGenWaveform : DFrameUnit {
    public enum WaveformType {
      Sine,
      Triangle,
      Saw,
      Pulse,
    }
    public enum RangeType {
      Bipolar,
      Unipolar,
    }

    [DoNotSerialize] public ValueInput Type;
    [DoNotSerialize] public ValueInput Range;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 4, 1)][ShortEditor] public ValueInput Gain;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(-4, 4, 0)][ShortEditor] public ValueInput Bias;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ClampMode(ClampMode.Wrap)][ShortEditor] public ValueInput Phase;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Shape;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1000, 0)][ShortEditor] public ValueInput Time;
    [DoNotSerialize] public ValueInput TimeUnit;
    [DoNotSerialize] public ValueInput CycleSync;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(-4, 4, 1)][ShortEditor] public ValueInput TimeMultiplier;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultSpeedMax, 1)][ShortEditor] public ValueInput Speed;

    private bool _driveTime = false;
    [Serialize][Inspectable] public bool DriveTime {
      get => _driveTime;
      set {
        _driveTime = value;
        PortsChanged();
      }
    }

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput forward;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput complement;

    protected override void Definition() {
      Type = ValueInput<WaveformType>(nameof(Type), WaveformType.Sine);
      Range = ValueInput<RangeType>(nameof(Range), RangeType.Bipolar);
      Gain = ValueInput<DValue>(nameof(Gain), 1.0);
      Bias = ValueInput<DValue>(nameof(Bias), 0.0);
      Phase = ValueInput<DValue>(nameof(Phase), 0.0);
      Shape = ValueInput<DValue>(nameof(Shape), 0.0);
      if (_driveTime) {
        Time = ValueInput<DValue>(nameof(Time), 0.0);
      } else {
        TimeUnit = ValueInput<TimeUnitType>(nameof(TimeUnit), TimeUnitType.Looped);
        CycleSync = ValueInput<bool>(nameof(CycleSync), false);
      }
      TimeMultiplier = ValueInput<DValue>(nameof(TimeMultiplier), 1.0);
      Speed = ValueInput<DValue>(nameof(Speed), 1.0);

      DValue ComputeFromFlow(Flow flow, bool complement) {
        double timeMultiplier = flow.GetValue<DValue>(TimeMultiplier);
        double time = _driveTime ? (flow.GetValue<DValue>(Time) * timeMultiplier)
                                 : flow.GetValue<TimeUnitType>(TimeUnit).GetTime(multiplier: timeMultiplier, sync: flow.GetValue<bool>(CycleSync));

        WaveformType type = flow.GetValue<WaveformType>(Type);
        RangeType range = flow.GetValue<RangeType>(Range);
        DValue gain = flow.GetValue<DValue>(Gain);
        DValue bias = flow.GetValue<DValue>(Bias);
        DValue phase = flow.GetValue<DValue>(Phase);
        DValue shape = flow.GetValue<DValue>(Shape);
        DValue speed = flow.GetValue<DValue>(Speed);

        int rows = Math.Max(gain.Rows, Math.Max(bias.Rows, Math.Max(phase.Rows, Math.Max(shape.Rows, speed.Rows))));
        DMutableValue result = new DMutableValue(rows, 1);
        for (int row = 0; row < rows; ++row) {
          double t = time * speed[row, 0] + phase[row, 0];
          double value;
          switch (type) {
            case WaveformType.Sine: {
              if (complement) {
                t += 0.25f;
              }
              value = Math.Sin(t * Math.PI * 2.0);
              break;
            }
            case WaveformType.Triangle: {
              if (complement) {
                t += 0.25f;
              }
              value = Math.Abs(t - Math.Floor(t) - 0.5) * -4.0 + 1.0;
              break;
            }
            default:
            case WaveformType.Saw: {
              value = (t - Math.Floor(t)) * 2.0 - 1.0;
              if (complement) {
                value = -value;
              }
              break;
            }
            case WaveformType.Pulse: {
              if (complement) {
                t += 0.5f;
              }
              value = (t - Math.Floor(t)) >= 0.5 ? 1.0 : -1.0;
              break;
            }
          }
          if (range == RangeType.Unipolar) {
            value = value * 0.5 + 0.5;
          }
          double output = value * gain[row, 0] + bias[row, 0];
          result[row, 0] = output;
        }
        return result.ToValue();
      }

      forward = ValueOutput<DValue>("A", DNodeUtils.CachePerFrame(flow => ComputeFromFlow(flow, false)));
      complement = ValueOutput<DValue>("B", DNodeUtils.CachePerFrame(flow => ComputeFromFlow(flow, true)));
    }
  }
}
