using System;
using Unity.VisualScripting;

namespace DNode {
  public class DRandomScalar : Unit {
    private const int _maxRetry = 100;
    private static System.Random _random = new System.Random();

    public enum NumberType {
      Float,
      Int,
      IntOther,
    }

    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput Min;
    [DoNotSerialize][PortLabelHidden][Scalar][ShortEditor] public ValueInput Max;
    [DoNotSerialize] public ValueInput Trigger;

    [Inspectable] public NumberType Type;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private int _previousValue = int.MinValue;

    protected override void Definition() {
      Min = ValueInput<DValue>(nameof(Min), 0.0);
      Max = ValueInput<DValue>(nameof(Max), 1.0);
      Trigger = ValueInput<bool>("Trigger", false);

      DEvent ComputeFromFlow(Flow flow) {
        bool trigger = flow.GetValue<bool>(Trigger);
        NumberType type = Type;

        DValue Compute() {
          double min = flow.GetValue<DValue>(Min);
          double max = flow.GetValue<DValue>(Max);

          switch (type) {
            default:
            case NumberType.Float: {
              double t = _random.NextDouble();
              return min * (1.0 - t) + max * t;
            }
            case NumberType.Int: {
              return _random.Next((int)Math.Round(min), (int)Math.Round(max));
            }
            case NumberType.IntOther: {
              for (int i = 0; i < _maxRetry; ++i) {
                int sample = _random.Next((int)Math.Round(min), (int)Math.Round(max));
                if (sample != _previousValue) {
                  _previousValue = sample;
                  return sample;
                }
              }
              return _previousValue;
            }
          }
        }

        return DEvent.Create(Compute, trigger);
      }
      result = ValueOutput<DEvent>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
