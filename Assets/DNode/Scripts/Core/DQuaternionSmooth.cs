using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DQuaternionSmooth : Unit {
    private const double _epsilon = 1e-6;

    private class Smoother {
      private struct History {
        public Quaternion StartValue;
        public Quaternion CurrentValue;
        public Quaternion TargetValue;
      }

      private History _history;
      private double _currentTime = 0.0;
      private bool _alive = false;

      public void Step(DValue input, int row, DMutableValue output, double deltaTime, double parameter, bool isEuler) {
        bool retrigger = false;
        Quaternion inputValue = (isEuler ? Quaternion.Euler(input.Vector3FromRow(row)) : input.QuaternionFromRow(row)).normalized;
        Quaternion historyValue = _history.TargetValue;
        if (Quaternion.Angle(inputValue, historyValue) > _epsilon) {
          retrigger = true;
        }
        if (retrigger) {
          if (_alive) {
            ref History history = ref _history;
            history.StartValue = history.CurrentValue;
            history.TargetValue = inputValue;
          } else {
            ref History history = ref _history;
            history.StartValue = history.TargetValue;
            history.TargetValue = inputValue;
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

            ref History history = ref _history;
            Quaternion value = Quaternion.Slerp(history.StartValue, inputValue, (float)t);
            history.CurrentValue = value;

            if (isEuler) {
              Vector3 euler = value.eulerAngles;
              output[row, 0] = euler.x;
              output[row, 1] = euler.y;
              output[row, 2] = euler.z;
            } else {
              output[row, 0] = value.x;
              output[row, 1] = value.y;
              output[row, 2] = value.z;
              output[row, 3] = value.w;
            }
          }
        }
        if (!_alive) {
          for (int i = 0; i < 4; ++i) {
            output[row, i] = input[row, i];
          }
        }
      }

      public Smoother(int columns, DValue input, int row) {
        Quaternion currentValue = input.QuaternionFromRow(row).normalized;
        _history = new History { StartValue = currentValue, CurrentValue = currentValue, TargetValue = currentValue };
      }
    }

    private readonly List<Smoother> _smoothers = new List<Smoother>();

    [DoNotSerialize][PortLabelHidden][NoEditor] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultEnvelopeTimeMax, 1.0)][LogScale(D3DConstants.DefaultEnvelopeTimeLogScale)][ShortEditor] public ValueInput Time;
    [DoNotSerialize][PortLabelHidden][Scalar][OneRange][ShortEditor] public ValueInput Easing;
    [DoNotSerialize] public ValueInput Reset;

    [Inspectable] public bool IsQuaternion;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Input = ValueInput<DValue>(nameof(Input), 0);
      Easing = ValueInput<DValue>(nameof(Easing), 0.0);
      Time = ValueInput<DValue>(nameof(Time), 1.0);
      Reset = ValueInput<bool>(nameof(Reset), false);

      DValue ComputeFromFlow(Flow flow) {
        if (flow.GetValue<bool>(Reset)) {
          _smoothers.Clear();
        }
        bool isQuaternion = IsQuaternion;
        double time = flow.GetValue<DValue>(Time);
        double easing = flow.GetValue<DValue>(Easing);
        DValue input = flow.GetValue<DValue>(Input);
        int rows = input.Rows;
        int columns = input.Columns;
        DMutableValue result = new DMutableValue(rows, columns);

        if (_smoothers.Count > rows) {
          _smoothers.RemoveRange(rows, _smoothers.Count - rows);
        }
        if (_smoothers.Count < rows) {
          while (_smoothers.Count < rows) {
            _smoothers.Add(new Smoother(columns, input, _smoothers.Count));
          }
        }

        double deltaTime = DScriptMachine.CurrentInstance.Transport.DeltaTime / Math.Max(_epsilon, time);
        for (int row = 0; row < rows; ++row) {
          _smoothers[row].Step(input, row, result, deltaTime, easing, !isQuaternion);
        }
        return result.ToValue();
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
