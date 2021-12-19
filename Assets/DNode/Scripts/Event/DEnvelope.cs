using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public class DEnvelope : Unit {
	  private const double DefaultAlpha = 0.00005;
	  private const double DefaultLinearAlpha = 5;
	  private const double SustainAlphaPower = 0.0001;
	  private const double MinConstantDecay = -1E-12;

	  private static readonly double LogAlphaMin = Math.Log(DefaultLinearAlpha, 2);
	  private static readonly double LogAlphaInterval = Math.Log(DefaultAlpha / DefaultLinearAlpha, 2);

    public enum ResetMode {
      Hard,
      Soft,
    }

    private class EnvelopeGenerator {
      private enum Stage {
        Attack,
        Decay,
        Sustain,
        Release,
        Complete,
      }

      private bool _isTriggerDown;
      private Stage _stage = Stage.Complete;
      private double _time;
      private double _expValue;
      private double _output;

      private struct ExponentialStage {
        public double Time;
        public double Alpha;
        public double TimeScale;
        public double C0;
      }

      private ExponentialStage _attackParams;
      private ExponentialStage _decayParams;
      private ExponentialStage _releaseParams;

      public EnvelopeGenerator() {
        ZeroExponentialStage(out _attackParams);
        ZeroExponentialStage(out _decayParams);
        ZeroExponentialStage(out _releaseParams);
      }

      public void Reset() {
			  _stage = Stage.Complete;
        _isTriggerDown = false;
        _time = 0.0;
        _expValue = 0.0;
        _output = 0.0;
      }

      public double Step(bool triggerDown, ResetMode resetMode, double attack, double attackAlpha, double decay, double decayAlpha, double sustain, double release, double releaseAlpha, double deltaTime) {
        _time += deltaTime;
        if (triggerDown != _isTriggerDown) {
          _isTriggerDown = triggerDown;
          if (!triggerDown) {
				    _stage = Stage.Release;
				    double startValue = _output;
				    _time = 0;
				    _expValue = startValue;
			    } else {
            bool wasActive = _stage != Stage.Complete;
				    _stage = Stage.Attack;
				    double startValue;
            if (resetMode == ResetMode.Hard) {
              startValue = 1.0;
            } else {
              startValue = wasActive ? (1.0f - _output) : 1.0f;
            }
				    _time = 0;
				    _expValue = startValue;
          }
        }
      
        switch (_stage) {
          case Stage.Attack: {
            double value = StepValue(_time, ref _expValue, attack, attackAlpha, deltaTime, ref _attackParams);
            if (value <= 0) {
              _time = 0;
              value = 0;
              _expValue = 1;
              _output = 1;
            } else {
              _output = 1 - value;
              return _output;
            }
            _stage = Stage.Decay;
            if (_time < decay) {
              return _output;
            }
            goto case Stage.Decay;
          }
          case Stage.Decay: {
            double value = StepValue(_time, ref _expValue, decay, decayAlpha, deltaTime, ref _decayParams);
            if (value <= 0) {
              _time = 0;
              value = 0;
              _expValue = 1;
              _output = sustain;
              _stage = Stage.Sustain;
            } else {
              _output = value * (1 - sustain) + sustain;
              return _output;
            }
            break;
          }
          case Stage.Sustain: {
            _output = sustain;
            return _output;
          }
          case Stage.Release: {
            double value = StepValue(_time, ref _expValue, release, releaseAlpha, deltaTime, ref _releaseParams);
            if (value <= 0) {
              _time = 0;
              value = 0;
              _expValue = 0;
              _output = 0;
              _stage = Stage.Complete;
            } else {
              _output = value;
              return _output;
            }
            break;
          }
          default:
            break;
        }

        return _output;
      }

		  private static double StepValue(double t, ref double expValue, double time, double alpha, double deltaTime, ref ExponentialStage stageParams) {
        if (Math.Abs(stageParams.Time - time) > UnityUtils.DefaultEpsilon ||
            Math.Abs(stageParams.Alpha - alpha) > UnityUtils.DefaultEpsilon) {
          stageParams.Time = time;
          stageParams.Alpha = alpha;
          ComputeScale(time, alpha, out stageParams.TimeScale, out stageParams.C0);
        }
        return ComputeValue(t, ref expValue, stageParams.TimeScale, stageParams.C0, deltaTime);
		  }

      private static void ZeroExponentialStage(out ExponentialStage stageParams) {
        stageParams.Time = 0;
        stageParams.Alpha = DefaultAlpha;
        ComputeScale(0, DefaultAlpha, out stageParams.TimeScale, out stageParams.C0);
      }

		  private static void ComputeScale(double time, double alphaParam, out double timeScale, out double c0) {
			  double logAlpha = Math.Max(0.0, Math.Min(1.0, alphaParam)) * LogAlphaInterval + LogAlphaMin;
			  double alpha = Math.Pow(2, logAlpha);
			  double effectiveTime = ProductLogApproximation(1 / alpha);
			  timeScale = (time <= UnityUtils.DefaultEpsilon || alpha <= UnityUtils.DefaultEpsilon) ? -1e8 : (-effectiveTime / time);
			  c0 = Math.Min(MinConstantDecay, alpha * timeScale);
		  }

		  private static double ComputeValue(double t, ref double expValue, double scale, double c0, double deltaTime) {
			  expValue += scale * expValue * deltaTime;
			  return expValue + c0 * t;
		  }

		  private static double ProductLogApproximation(double x) {
			  double wj = ProductLogApproximationStep(x < 1 ? 0 : Math.Log(x), x);
			  wj = ProductLogApproximationStep(wj, x);
			  return wj;
		  }
 
		  private static double ProductLogApproximationStep(double x, double z) {
			  double ex = Math.Exp(x);
			  double v = x * ex - z;
			  return x - v / (ex * (x + 1) - ((x+2)*(v)) / (2*x+2));
		  }
    }

    [DoNotSerialize] public ValueInput Trigger;
    [DoNotSerialize] public ValueInput MultiTrigger;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultEnvelopeTimeMax, 1.0)][LogScale(D3DConstants.DefaultEnvelopeTimeLogScale)][ShortEditor]  public ValueInput AttackTime;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.0)][ShortEditor] public ValueInput AtkEasing;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultEnvelopeTimeMax, 1.0)][LogScale(D3DConstants.DefaultEnvelopeTimeLogScale)][ShortEditor] public ValueInput DecayTime;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.0)][ShortEditor] public ValueInput DecEasing;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.0)][ShortEditor]  public ValueInput Sustain;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultEnvelopeTimeMax, 1.0)][LogScale(D3DConstants.DefaultEnvelopeTimeLogScale)][ShortEditor]  public ValueInput ReleaseTime;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.0)][ShortEditor] public ValueInput RlsEasing;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 1.0)][ShortEditor] public ValueInput Gain;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultSpeedMax, 1.0)][ShortEditor] public ValueInput Speed;
    [DoNotSerialize] public ValueInput Reset;
    
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
    [Inspectable] public ResetMode OnTriggerResetMode;
    [Inspectable] public bool UseAbsoluteTime;

    private double _lastAbsoluteTime = 0.0;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private List<EnvelopeGenerator> _envelopes = new List<EnvelopeGenerator>();

    protected override void Definition() {
      if (_useMultiTrigger) {
        MultiTrigger = ValueInput<DValue>(nameof(MultiTrigger));
      } else {
        Trigger = ValueInput<bool>(nameof(Trigger), false);
      }
      AttackTime = ValueInput<DValue>(nameof(AttackTime), 0.0);
      DecayTime = ValueInput<DValue>(nameof(DecayTime), 1.0);
      Sustain = ValueInput<DValue>(nameof(Sustain), 0.0);
      ReleaseTime = ValueInput<DValue>(nameof(ReleaseTime), 1.0);
      AtkEasing = ValueInput<DValue>(nameof(AtkEasing), 0.5);
      DecEasing = ValueInput<DValue>(nameof(DecEasing), 0.5);
      RlsEasing = ValueInput<DValue>(nameof(RlsEasing), 0.5);
      Gain = ValueInput<DValue>(nameof(Gain), 1.0);
      Speed = ValueInput<DValue>(nameof(Speed), 1.0);
      Reset = ValueInput<bool>(nameof(Reset), false);

      DValue ComputeFromFlow(Flow flow) {
        DValue input;
        if (_useMultiTrigger) {
          if (MultiTrigger.hasAnyConnection) {
            input = flow.GetValue<DValue>(MultiTrigger);
          } else {
            input = 0.0;
          }
        } else {
          input = flow.GetValue<bool>(Trigger) ? 1.0 : 0.0;
        }

        int rows = input.Rows;
        bool reset = flow.GetValue<bool>(Reset);
        ResetMode onTriggerResetMode = OnTriggerResetMode;
        DValue attack = flow.GetValue<DValue>(AttackTime);
        DValue attackAlpha = flow.GetValue<DValue>(AtkEasing);
        DValue decay = flow.GetValue<DValue>(DecayTime);
        DValue decayAlpha = flow.GetValue<DValue>(DecEasing);
        DValue sustain = flow.GetValue<DValue>(Sustain);
        DValue release = flow.GetValue<DValue>(ReleaseTime);
        DValue releaseAlpha = flow.GetValue<DValue>(RlsEasing);
        DValue gain = flow.GetValue<DValue>(Gain);
        DValue speed = flow.GetValue<DValue>(Speed);
        DMutableValue result = new DMutableValue(rows, 1);

        if (_envelopes.Count > rows) {
          _envelopes.RemoveRange(rows, _envelopes.Count - rows);
        }
        if (_envelopes.Count < rows) {
          while (_envelopes.Count < rows) {
            _envelopes.Add(new EnvelopeGenerator());
          }
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
        for (int row = 0; row < rows; ++row) {
          var envelope = _envelopes[row];
          if (reset) {
            envelope.Reset();
          }
          double output =
              envelope.Step(input[row, 0] != 0, onTriggerResetMode,
                            attack[row, 0], attackAlpha[row, 0],
                            decay[row, 0], releaseAlpha[row, 0],
                            sustain[row, 0],
                            release[row, 0], releaseAlpha[row, 0],
                            deltaTime * speed[row, 0]);
          result[row, 0] = output * gain[row, 0];
        }
        return result.ToValue();
      }
      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
