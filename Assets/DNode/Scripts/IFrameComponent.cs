using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {

  public interface IFrameComponent {
    void OnStartFrame();
    void OnEndFrame();
    void Dispose();
  }

  public interface IFrameComponentField {
    void Awake();
    void OnEndFrame(out bool wasChanged);
    void ResetToInitialValue();
  }

  public class FrameComponentField<TComponent, TValue> : IFrameComponentField {
    private static readonly IEqualityComparer<TValue> _comparer = EqualityComparer<TValue>.Default;
    private readonly Func<TComponent, TValue> _getter;
    private readonly Action<TComponent, TValue> _setter;
    private readonly TComponent _component;

    private TValue _initialValue = default;
    private TValue _currentValue;
    private bool _isDirty = false;
    private bool _wasChanged = false;
    private int _currentFrameNumber = 0;

    public FrameComponentField(TComponent comonent, Func<TComponent, TValue> getter, Action<TComponent, TValue> setter) {
      _component = comonent;
      _getter = getter;
      _setter = setter;
    }

    public TValue Value {
      get {
        return _getter.Invoke(_component);
      }
      set {
        _currentFrameNumber = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
        if (_comparer.Equals(_currentValue, value)) {
          return;
        }
        if (!_isDirty) {
          _initialValue = _getter.Invoke(_component);
        }
        _isDirty = true;
        _wasChanged = true;
        _currentValue = value;
        _setter.Invoke(_component, value);
      }
    }

    public void MaybeSetValue<TOther>(TOther? other) where TOther : struct, TValue {
      if (other == null) {
        return;
      }
      Value = other.Value;
    }

    public void MaybeSetValue<TOther>(TOther other) where TOther : class, TValue {
      if (other == null) {
        return;
      }
      Value = other;
    }

    public void Awake() {
      _initialValue = _getter(_component);
      _currentValue = _initialValue;
    }

    public void OnEndFrame(out bool wasChanged) {
      if (!_isDirty) {
        wasChanged = false;
        return;
      }
      int currentFrame = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
      if (_currentFrameNumber != currentFrame) {
        _setter.Invoke(_component, _initialValue);
        _currentValue = _initialValue;
        wasChanged = true;
        _isDirty = false;
        _wasChanged = false;
      } else {
        wasChanged = _wasChanged;
        _wasChanged = false;
      }
    }

    public void ResetToInitialValue() {
      if (!_isDirty) {
        return;
      }
      _setter.Invoke(_component, _initialValue);
      _currentValue = _initialValue;
      _isDirty = false;
      _wasChanged = false;
    }
  }

  public abstract class FrameComponentBase : MonoBehaviour, IFrameComponent {
    private IFrameComponentField[] _fields;

    public void Dispose() {}

    protected void Awake() {
      _fields = GetFields().ToArray();
      foreach (IFrameComponentField field in _fields) {
        field.Awake();
      }
    }

    protected abstract IEnumerable<IFrameComponentField> GetFields();

    public void ResetToInitialValues() {
      foreach (IFrameComponentField field in _fields) {
        field.ResetToInitialValue();
      }
    }

    public virtual void OnStartFrame() {
    }

    public virtual void OnEndFrame() {
      OnCommitFields(out var _);
    }

    protected virtual void OnCommitFields(out bool wasChanged) {
      bool wasAnyChanged = false;
      foreach (IFrameComponentField field in _fields) {
        field.OnEndFrame(out bool wasFieldChanged);
        wasAnyChanged = wasAnyChanged || wasFieldChanged;
      }
      wasChanged = wasAnyChanged;
    }
  }
}