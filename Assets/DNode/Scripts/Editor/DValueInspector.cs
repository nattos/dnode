using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace DNode {
  [Inspector(typeof(DValue))]
  public class DValueInspector : Inspector {
    private static readonly List<double> _staticValues = new List<double>();

    private readonly AttributeCache _attributeCache = new AttributeCache();

    public DValueInspector(Metadata metadata) : base(metadata) {}

    private struct FieldState {
      public bool Triggered;
      public bool Editable;
      public bool Highlighted;
      public bool Hovered;
    }

    private struct VectorState {
      public bool HadFirstDim;
      public bool IsGroupDrag;
      public float DragDelta;
    }

    private enum PortState {
      NotConnected,
      ConnectedUnknown,
      ConnectedValue,
      ConnectedEvent,
      ConnectedEventDeferred,
      ConnectedEventTriggered,
    }

    private struct ValueState {
      public PortState PortState;
      public double Min;
      public double Max;
      public bool IsLog;
      public double LogScalingFactor;
      public ClampMode ClampMode;
      public bool ShowLabel;
      public bool AllowFullLabel;

      public bool IsEditable => PortState == PortState.NotConnected;
      public bool ConnectedToEvent => PortState == PortState.ConnectedEvent || PortState == PortState.ConnectedEventTriggered || PortState == PortState.ConnectedEventDeferred;
      public bool HasValue => PortState != PortState.ConnectedUnknown && PortState != PortState.ConnectedEventDeferred;
    }

    public static bool DValueField(Rect rect, Metadata metadata, DValue inValue, AttributeCache attributeCache, out DValue outValue) {
      outValue = inValue;
      if (attributeCache.TryGetAttribute<NoEditorAttribute>(metadata, out _)) {
        return false;
      }

      PortState portState;
      string label = UnityEditorUtils.GetFieldLabel(metadata, attributeCache);
      if (UnityEditorUtils.IsFieldEditable(metadata)) {
        portState = PortState.NotConnected;
      } else {
        ValueConnection valueConnection = (metadata.parent?.parent?.value as ValueInput)?.connection;
        ValueConnection.DebugData debugData = valueConnection == null ? null : LudiqGraphsEditorUtility.editedContext.value?.reference?.GetElementDebugData<ValueConnection.DebugData>(valueConnection);
        object debugValue = debugData?.lastValue;
        if (debugValue == null) {
          Type sourceType = valueConnection?.source?.type ?? typeof(void);
          if (typeof(DEvent).IsAssignableFrom(sourceType)) {
            portState = PortState.ConnectedEvent;
          } else if (ConversionUtility.CanConvert(sourceType, typeof(DValue), guaranteed: false)) {
            portState = PortState.ConnectedValue;
          } else {
            portState = PortState.ConnectedUnknown;
          }
        } else {
          if (typeof(DEvent).IsAssignableFrom(debugValue) && UnityUtils.TryConvert(debugValue, out DEvent devent, guaranteed: false)) {
            portState = devent.IsTriggered ? PortState.ConnectedEventTriggered : devent.ValueProvider != null ? PortState.ConnectedEventDeferred : PortState.ConnectedEvent;
            inValue = devent.ImmediateValue;
          } else if (UnityUtils.TryConvert(debugValue, out DValue dvalue, guaranteed: false)) {
            portState = PortState.ConnectedValue;
            inValue = dvalue;
          } else {
            portState = PortState.ConnectedUnknown;
          }
        }
      }
      if (portState == PortState.ConnectedUnknown) {
        EditorGUI.LabelField(rect, label);
        return false;
      }

      _staticValues.Clear();
      attributeCache.TryGetAttribute<RangeAttribute>(metadata, out var rangeAttrib);
      attributeCache.TryGetAttribute<VectorAttribute>(metadata, out var vectorAttrib);
      int dims = vectorAttrib?.Dims ?? 1;
      float dimPartWidth = rect.width / dims;

      bool isBool = attributeCache.TryGetAttribute<BooleanAttribute>(metadata, out _);
      attributeCache.TryGetAttribute<ClampModeAttribute>(metadata, out var clampAttrib);
      bool isLog = attributeCache.TryGetAttribute<LogScaleAttribute>(metadata, out var logScaleAttrib);

      ValueState state = new ValueState {
          PortState = portState,
          Min = rangeAttrib?.Min ?? D3DConstants.DefaultGenericMathMin,
          Max = rangeAttrib?.Max ?? D3DConstants.DefaultGenericMathMax,
          IsLog = isLog,
          LogScalingFactor = logScaleAttrib?.ScalingFactor ?? 1.0,
          ClampMode = clampAttrib?.ClampMode ?? ClampMode.Clamp,
          ShowLabel = true,
      };

      EditorGUI.BeginChangeCheck();
      if (attributeCache.TryGetAttribute<ColorAttribute>(metadata, out _)) {
        Color outColor = EditorGUI.ColorField(rect, label: GUIContent.none, inValue, showEyedropper: true, showAlpha: true, hdr: true);
        _staticValues.Add(outColor.r);
        _staticValues.Add(outColor.g);
        _staticValues.Add(outColor.b);
        _staticValues.Add(outColor.a);
      } else {
        Rect pixelRect = UnityEditorUtils.PixelAlignRect(rect);
        bool drawFullLabel = false;
        VectorState vectorState = default;
        for (int i = 0; i < dims; ++i) {
          string partLabel = i == 0 ? label : i == 1 ? "Y" : i == 2 ? "Z" : i == 3 ? "W" : "";
          state.AllowFullLabel = i == 0;
          Rect dimRect = pixelRect;
          dimRect.x += i * dimPartWidth;
          dimRect.width = dimPartWidth;
          double defaultValue = (i == 0 ? rangeAttrib?.DefaultValue0 : i == 1 ? rangeAttrib?.DefaultValue1 : i == 2 ? rangeAttrib?.DefaultValue2 : i == 3 ? rangeAttrib?.DefaultValue3 : null) ?? 0.0;
          if (isBool) {
            _staticValues.Add(BooleanToggle(dimRect, partLabel, inValue[0, i], state));
          } else {
            _staticValues.Add(Slider(dimRect, partLabel, inValue[0, i], defaultValue, state, ref vectorState, out bool localDrawFullLabel));
            if (localDrawFullLabel) {
              drawFullLabel = true;
              state.ShowLabel = false;
            }
          }
        }
        if (drawFullLabel) {
          EditorGUI.DrawRect(GetSliderInnerRect(pixelRect), _sliderBarOverlayColor);
          DrawSliderLabel(pixelRect, label, isFullWidth: true, isVisible: true);
        }
      }
      if (EditorGUI.EndChangeCheck()) {
        if (!state.IsEditable) {
          return false;
        }
        outValue = (DValue)_staticValues.ToArray();
        return true;
      } else {
        return false;
      }
    }

    public static (float width, float height) GetDValidFieldSize(Metadata metadata, AttributeCache attributeCache) {
      if (attributeCache.TryGetAttribute<NoEditorAttribute>(metadata, out _)) {
        return (0, EditorGUIUtility.singleLineHeight);
      }
      if (attributeCache.TryGetAttribute<ShortEditorAttribute>(metadata, out _) ||
          !attributeCache.TryGetAttribute<RangeAttribute>(metadata, out _)) {
        return (100, EditorGUIUtility.singleLineHeight);
      }
      return (200, EditorGUIUtility.singleLineHeight);
    }

    protected override void OnGUI(Rect position, GUIContent label) {
      // position = BeginLabeledBlock(metadata, position, label);
      EditorGUI.BeginChangeCheck();
      var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

      DValueField(rect, metadata, (DValue)metadata.value, _attributeCache, out DValue outValue);

      // if (EndBlock(metadata)) {
      if (EditorGUI.EndChangeCheck()) {
        metadata.RecordUndo();
        metadata.value = (DValue)outValue;
      }
      _staticValues.Clear();
    }

    protected override void OnEditorPrefGUI(Rect position, GUIContent label) {}

    protected override float GetHeight(float width, GUIContent label) {
      return GetDValidFieldSize(metadata, _attributeCache).height;
    }

    public override float GetAdaptiveWidth() {
      return GetDValidFieldSize(metadata, _attributeCache).width;
    }

    private const double _sliderFineDragDistance = 4000.0;
    private const double _defaultLogSliderDoublings = 16.0;
    private const float _sliderBorderWidth = 1.0f;
    private const float _sliderTextPadding = 2.0f;

    private static readonly Color _sliderBarColor = new Color(0xB5 / 255.0f, 0x0C / 255.0f, 0x74 / 255.0f);
    private static readonly Color _sliderBarBackground = new Color(0x30 / 255.0f, 0x30 / 255.0f, 0x30 / 255.0f);
    private static readonly Color _sliderBarBorder = new Color(0x30 / 255.0f, 0x30 / 255.0f, 0x30 / 255.0f);
    private static readonly Color _sliderBarHighlightBorder = new Color(0x15 / 255.0f, 0x15 / 255.0f, 0x15 / 255.0f);
    private static readonly Color _sliderBarHoveredBorder = new Color(0xA5 / 255.0f, 0xA5 / 255.0f, 0xA5 / 255.0f);
    private static readonly Color _sliderBarOverlayColor = new Color(0x30 / 255.0f, 0x30 / 255.0f, 0x30 / 255.0f, 0.65f);

    private static readonly Color _sliderBarConnectedColor = new Color(0x64 / 255.0f, 0x64 / 255.0f, 0x64 / 255.0f);
    private static readonly Color _sliderBarConnectedBackground = new Color(0x50 / 255.0f, 0x50 / 255.0f, 0x50 / 255.0f);
    private static readonly Color _sliderBarConnectedBorder = new Color(0x35 / 255.0f, 0x35 / 255.0f, 0x35 / 255.0f);
    private static readonly Color _sliderBarConnectedHighlightBorder = new Color(0xA6 / 255.0f, 0xE9 / 255.0f, 0x56 / 255.0f);
    private static readonly Color _sliderBarConnectedEventBorder = new Color(0x64 / 255.0f, 0x8B / 255.0f, 0x34 / 255.0f);
    
    private static readonly int _dsliderControlIDHint = "DSlider".GetHashCode();
    private static bool _numberDragState = false;
    private static bool _numberDragVector = false;
    private static double _numberDragStartValue;
    private static Vector2 _numberDragStartPosition;
    private static Vector2 _numberDragDelta;

    private static Rect GetSliderInnerRect(Rect rect) {
      return UnityEditorUtils.Inset(rect, _sliderBorderWidth);
    }

    private static void DrawSliderBackground(Rect rect, double value, ValueState state, FieldState fieldState) {
      Color borderColor;
      Color backgroundColor;
      Color barColor;
      if (state.PortState == PortState.NotConnected) {
        backgroundColor = _sliderBarBackground;
        borderColor = fieldState.Highlighted ? _sliderBarHighlightBorder : fieldState.Hovered ? _sliderBarHoveredBorder : _sliderBarBorder;
        barColor = _sliderBarColor;
      } else if (state.ConnectedToEvent) {
        backgroundColor = _sliderBarConnectedBackground;
        borderColor = state.PortState == PortState.ConnectedEventTriggered ? _sliderBarConnectedHighlightBorder : _sliderBarConnectedEventBorder;
        barColor = _sliderBarConnectedColor;
      } else {
        backgroundColor = _sliderBarConnectedBackground;
        borderColor = _sliderBarConnectedBorder;
        barColor = _sliderBarConnectedColor;
      }
      EditorGUI.DrawRect(rect, borderColor);
      Rect innerRect = GetSliderInnerRect(rect);
      EditorGUI.DrawRect(innerRect, backgroundColor);
      if (state.HasValue) {
        Rect valueRect = innerRect;
        valueRect.width *= (float)Math.Max(0.0, Math.Min(1.0, ToTSpace(value, state.Min, state.Max, state.IsLog, state.LogScalingFactor)));
        EditorGUI.DrawRect(valueRect, barColor);
      }
    }

    private static void DrawSliderLabel(Rect rect, string label, bool isFullWidth, bool isVisible) {
      if (!isVisible) {
        // Note: If this is zero, Unity doesn't record the record the existence of the control at
        // all, breaking the ordering of controls and their IDs. This messes up certain dragging
        // operations.
        rect.width = 1.0f;
      }
      EditorGUI.LabelField(SliderLabelRect(rect, isFullWidth), label);
    }
    private static Rect SliderLabelRect(Rect rect, bool isFullWidth) {
      Rect innerRect = GetSliderInnerRect(rect);
      Rect labelRect = UnityEditorUtils.Inset(innerRect, _sliderTextPadding, 0.0f);
      if (!isFullWidth) {
        labelRect.width *= 0.5f;
      }
      return labelRect;
    }
    private static float SliderTextField(Rect rect, double value, bool isFullWidth) {
      return EditorGUI.FloatField(SliderTextRect(rect, isFullWidth), (float)value, EditorStyles.label);
    }
    private static Rect SliderTextRect(Rect rect, bool isFullWidth) {
      Rect innerRect = GetSliderInnerRect(rect);
      Rect textRect = UnityEditorUtils.Inset(innerRect, _sliderTextPadding, 0.0f);
      if (!isFullWidth) {
        textRect.x += textRect.width * 0.5f;
        textRect.width *= 0.5f;
      }
      return textRect;
    }

    private static double BooleanToggle(Rect rect, string label, double value, ValueState state) {
      var controlId = GUIUtility.GetControlID(_dsliderControlIDHint, FocusType.Passive, rect);
      FieldState fieldState = new FieldState {
          Highlighted = GUIUtility.keyboardControl == controlId,
          Hovered = rect.Contains(e.mousePosition),
      };

      state.Min = 0.0;
      state.Max = 1.0;

      bool boolValue = value != 0.0;
      DrawSliderBackground(rect, boolValue ? 1.0 : 0.0, state, fieldState);

      EditorGUI.BeginChangeCheck();
      switch (e.GetTypeForControl(controlId)) {
        case EventType.MouseDown: {
          if (!rect.Contains(e.mousePosition) || e.button != (int)MouseButton.Left) {
            break;
          }
          if (state.IsEditable) {
            boolValue = !boolValue;
            GUI.changed = true;
          }
          e.Use();
          break;
        }
      }

      DrawSliderLabel(rect, label, isFullWidth: true, isVisible: true);
      if (EditorGUI.EndChangeCheck()) {
        return boolValue ? 1.0 : 0.0;
      }
      return value;
    }

    private static double Slider(Rect rect, string label, double value, double defaultValue, ValueState state, ref VectorState vectorState, out bool drawFullLabel) {
      var controlId = GUIUtility.GetControlID(_dsliderControlIDHint, FocusType.Keyboard, rect);
      FieldState fieldState = new FieldState {
          Highlighted = GUIUtility.keyboardControl == controlId,
          Hovered = rect.Contains(e.mousePosition),
      };

      DrawSliderBackground(rect, value, state, fieldState);

      EditorGUI.BeginChangeCheck();
      if (e.clickCount < 2) {
        DragNumber(rect, controlId, ref value, defaultValue, state, fieldState, ref vectorState);
        vectorState.HadFirstDim = true;
      } else {
        GUIUtility.hotControl = 0;
        GUIUtility.keyboardControl = 0;
      }
      bool isDraggingValue = DragNumberIsDragging(controlId);
      bool forceLabels = e.alt && e.CtrlOrCmd();
      drawFullLabel = !forceLabels && state.AllowFullLabel && !isDraggingValue && UnityEditorUtils.Inset(SliderLabelRect(rect, isFullWidth: false), -_sliderBorderWidth).Contains(e.mousePosition);

      bool valueFullWidth = state.HasValue && (e.alt || isDraggingValue || (!drawFullLabel && fieldState.Hovered));
      bool showLabel = forceLabels || (!valueFullWidth && state.ShowLabel && !drawFullLabel);
      bool showValue = !forceLabels && state.HasValue;
      DrawSliderLabel(rect, label, isFullWidth: !showValue, isVisible: showLabel);
      float floatValue = showValue ? SliderTextField(rect, value, valueFullWidth) : (float)value;
      if (EditorGUI.EndChangeCheck()) {
        return floatValue;
      }
      return value;
    }

    private static bool DragNumberIsDragging(int controlId) {
      return _numberDragState && GUIUtility.hotControl == controlId;
    }

    private static void DragNumber(Rect rect, int controlId, ref double value, double defaultValue, ValueState state, FieldState fieldState, ref VectorState vectorState) {
      if (vectorState.IsGroupDrag) {
        if (state.IsEditable) {
          if (e.CtrlOrCmd()) {
            state.ClampMode = ClampMode.None;
          }
          value = ComputeSliderValueFromDelta(rect, vectorState.DragDelta, value, state);
          GUI.changed = true;
        }
        return;
      }
      switch (e.GetTypeForControl(controlId)) {
        case EventType.MouseDown: {
          if (!rect.Contains(e.mousePosition) || e.button != (int)MouseButton.Left) {
            break;
          }

          EditorGUIUtility.editingTextField = false;
          GUIUtility.hotControl = controlId;
          GUIUtility.keyboardControl = controlId;

          _numberDragState = true;
          _numberDragVector =  !vectorState.HadFirstDim && e.shift && e.CtrlOrCmd();
          _numberDragStartValue = value;
          _numberDragStartPosition = e.mousePosition;
          _numberDragDelta = Vector2.zero;

          e.Use();
          EditorGUIUtility.SetWantsMouseJumping(e.shift ? 1 : 0);
          break;
        }

        case EventType.MouseUp: {
          if (GUIUtility.hotControl != controlId || !_numberDragState) {
            break;
          }

          GUIUtility.hotControl = 0;
          _numberDragState = false;
          e.Use();
          EditorGUIUtility.SetWantsMouseJumping(0);
          break;
        }

        case EventType.MouseDrag: {
          if (GUIUtility.hotControl != controlId) {
            break;
          }

          bool isShift = e.shift || _numberDragVector;
          EditorGUIUtility.SetWantsMouseJumping(isShift ? 1 : 0);

          if (state.IsEditable) {
            if (e.CtrlOrCmd()) {
              state.ClampMode = ClampMode.None;
            }
            if (isShift) {
              value = ComputeSliderValueFromDelta(rect, HandleUtility.niceMouseDelta, value, state);
              if (_numberDragVector) {
                vectorState.IsGroupDrag = true;
                vectorState.DragDelta = HandleUtility.niceMouseDelta;
              }
            } else {
              value = ComputeSliderValue(rect, e.mousePosition, state);
            }
            GUI.changed = true;
          }
          e.Use();
          break;
        }

        case EventType.KeyDown: {
          if (GUIUtility.hotControl != controlId && GUIUtility.keyboardControl != controlId) {
            break;
          }

          if (e.keyCode == KeyCode.Escape && _numberDragState) {
            if (state.IsEditable) {
              value = _numberDragStartValue;
              GUI.changed = true;
            }
            GUIUtility.hotControl = 0;
            e.Use();
          } else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) {
            if (state.IsEditable) {
              value = defaultValue;
              GUI.changed = true;
            }
            GUIUtility.hotControl = 0;
            e.Use();
          }
          break;
        }
      }
    }
    
    private static double ComputeSliderValueFromDelta(Rect rect, float delta, double oldValue, ValueState state) {
      double t = ToTSpace(oldValue, state.Min, state.Max, state.IsLog, state.LogScalingFactor);
      if (t < 0.0) {
        t += (Math.Floor(-t) + 1.0) * delta / _sliderFineDragDistance;
      } else if (t > 1.0) {
        t += (Math.Floor(t - 1.0) + 1.0) * delta / _sliderFineDragDistance;
      } else {
        t += delta / _sliderFineDragDistance;
      }
      return FromTSpace(t, state.Min, state.Max, state.IsLog, state.LogScalingFactor, state.ClampMode);
    }

    private static double ComputeSliderValue(Rect rect, Vector2 mousePoint, ValueState state) {
      double t = (mousePoint.x - (double)rect.x + (double)_numberDragStartPosition.y - mousePoint.y) / (double)rect.width;
      return FromTSpace(t, state.Min, state.Max, state.IsLog, state.LogScalingFactor, state.ClampMode);
    }

    private static double ToTSpace(double value, double min, double max, bool isLog, double logScalingFactor) {
      double range = max - min;
      double t = (value - min) / range;
      if (isLog) {
        double logPower = _defaultLogSliderDoublings * logScalingFactor;
        return Math.Log(Math.Max(UnityUtils.DefaultEpsilon, t * (logPower - 1.0) + 1.0), logPower);
      } else {
        return t;
      }
    }

    private static double FromTSpace(double t, double min, double max, bool isLog, double logScalingFactor, ClampMode clampMode) {
      if (isLog) {
        double logPower = _defaultLogSliderDoublings * logScalingFactor;
        t = (Math.Pow(logPower, t) - 1.0) / (logPower - 1.0);
      }
      if (clampMode == ClampMode.Clamp) {
        t = UnityUtils.Clamp(t);
      } else if (clampMode == ClampMode.Wrap) {
        t -= Math.Floor(t);
      }
      return UnityUtils.Lerp(min, max, t);
    }
  }
}
