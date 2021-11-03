using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace DNode {
  public class DSmooth : Unit {
    private const double _epsilon = UnityUtils.DefaultEpsilon;

    private class Smoother {
      private struct History {
        public double StartValue;
        public double CurrentValue;
        public double TargetValue;
      }

      private History[] _history;
      private double _currentTime = 0.0;
      private bool _alive = false;

      public void Step(DValue input, int row, DMutableValue output, double deltaTime, double parameter) {
        bool retrigger = false;
        int columns = _history.Length;
        for (int i = 0; i < columns; ++i) {
          ref History history = ref _history[i];
          double inputValue = input[row, i];
          if (Math.Abs(history.TargetValue - inputValue) > _epsilon) {
            retrigger = true;
            break;
          }
        }
        if (retrigger) {
          if (_alive) {
            for (int i = 0; i < columns; ++i) {
              ref History history = ref _history[i];
              double inputValue = input[row, i];
              history.StartValue = history.CurrentValue;
              history.TargetValue = inputValue;
            }
          } else {
            for (int i = 0; i < columns; ++i) {
              ref History history = ref _history[i];
              double inputValue = input[row, i];
              history.StartValue = history.TargetValue;
              history.TargetValue = inputValue;
            }
          }
          _currentTime = 0.0;
          _alive = true;
        }
        if (_alive) {
          _currentTime += deltaTime;
          if (_currentTime >= 1.0) {
            _alive = false;
          } else {
            double pow = parameter > 0.0 ? (1 / (1 + parameter)) : (1 - parameter);
            double preT = UnityUtils.Smoothstep(_currentTime);
            double t = Math.Pow(preT, pow);
            double invT = 1.0 - t;
            for (int i = 0; i < columns; ++i) {
              ref History history = ref _history[i];
              double value = history.StartValue * invT + input[row, i] * t;
              history.CurrentValue = value;
              output[row, i] = value;
            }
          }
        }
        if (!_alive) {
          for (int i = 0; i < columns; ++i) {
            output[row, i] = input[row, i];
          }
        }
      }

      public Smoother(int columns, DValue input, int row) {
        _history = new History[columns];
        for (int i = 0; i < columns; ++i) {
          double currentValue = input[row, i];
          _history[i] = new History { StartValue = currentValue, CurrentValue = currentValue, TargetValue = currentValue };
        }
      }

      public void ResizeColumns(int columns, DValue input, int row) {
        History[] oldHistory = _history;
        int oldColumns = oldHistory.Length;
        History[] newHistory = _history;
        Array.Resize(ref newHistory, columns);
        if (oldColumns < columns) {
          for (int i = oldColumns; i < columns; ++i) {
            double currentValue = input[row, i];
            newHistory[i] = new History { StartValue = currentValue, CurrentValue = currentValue, TargetValue = currentValue };
          }
        }
        _history = newHistory;
      }
    }

    private readonly List<Smoother> _smoothers = new List<Smoother>();
    private int _smootherColumns = 0;

    [DoNotSerialize][PortLabelHidden][NoEditor] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultEnvelopeTimeMax, 1.0)][LogScale(D3DConstants.DefaultEnvelopeTimeLogScale)][ShortEditor] public ValueInput Time;
    [DoNotSerialize][PortLabelHidden][Scalar][OneRange][ShortEditor] public ValueInput Easing;
    [DoNotSerialize] public ValueInput Reset;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Input = ValueInput<DValue>(nameof(Input), 0);
      Time = ValueInput<DValue>(nameof(Time), 1.0);
      Easing = ValueInput<DValue>(nameof(Easing), 0.0);
      Reset = ValueInput<bool>(nameof(Reset), false);

      DValue ComputeFromFlow(Flow flow) {
        if (flow.GetValue<bool>(Reset)) {
          _smoothers.Clear();
        }
        double time = flow.GetValue<DValue>(Time);
        double easing = flow.GetValue<DValue>(Easing);
        DValue input = flow.GetValue<DValue>(Input);
        int rows = input.Rows;
        int columns = input.Columns;
        DMutableValue result = new DMutableValue(rows, columns);

        if (_smoothers.Count > rows) {
          _smoothers.RemoveRange(rows, _smoothers.Count - rows);
        }
        if (_smootherColumns != columns) {
          for (int i = 0; i < _smoothers.Count; ++i) {
            _smoothers[i].ResizeColumns(columns, input, i);
          }
          _smootherColumns = columns;
        }
        if (_smoothers.Count < rows) {
          while (_smoothers.Count < rows) {
            _smoothers.Add(new Smoother(columns, input, _smoothers.Count));
          }
        }

        double deltaTime = DScriptMachine.CurrentInstance.Transport.DeltaTime / Math.Max(_epsilon, time);
        for (int row = 0; row < rows; ++row) {
          _smoothers[row].Step(input, row, result, deltaTime, easing);
        }
        return result.ToValue();
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
