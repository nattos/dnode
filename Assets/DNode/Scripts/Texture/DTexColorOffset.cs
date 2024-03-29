﻿using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexColorOffset : DTexUnaryBlitUnit {
    private int _ColorBasisR = Shader.PropertyToID("_ColorBasisR");
    private int _ColorBasisG = Shader.PropertyToID("_ColorBasisG");
    private int _ColorBasisB = Shader.PropertyToID("_ColorBasisB");
    private int _HueShift = Shader.PropertyToID("_HueShift");
    private int _ShiftR = Shader.PropertyToID("_ShiftR");
    private int _ShiftG = Shader.PropertyToID("_ShiftG");
    private int _ShiftB = Shader.PropertyToID("_ShiftB");
    private int _AlphaR = Shader.PropertyToID("_AlphaR");
    private int _AlphaG = Shader.PropertyToID("_AlphaG");
    private int _AlphaB = Shader.PropertyToID("_AlphaB");

    [DoNotSerialize][Color] public ValueInput ColorBasisR;
    [DoNotSerialize][Color] public ValueInput ColorBasisG;
    [DoNotSerialize][Color] public ValueInput ColorBasisB;
    [DoNotSerialize][PortLabelHidden][Scalar][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput HueShift;
    [DoNotSerialize][PortLabelHidden][Vector2][Range(-2, 2, 0)] public ValueInput ShiftR;
    [DoNotSerialize][PortLabelHidden][Vector2][Range(-2, 2, 0)] public ValueInput ShiftG;
    [DoNotSerialize][PortLabelHidden][Vector2][Range(-2, 2, 0)] public ValueInput ShiftB;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput AlphaR;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput AlphaG;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput AlphaB;

    private bool _useCustomColorBasis = false;
    [Serialize][Inspectable] public bool UseCustomColorBasis {
      get => _useCustomColorBasis;
      set {
        _useCustomColorBasis = value;
        PortsChanged();
      }
    }
    private bool _useAlphaPerChannel = false;
    [Serialize][Inspectable] public bool UseAlphaPerChannel {
      get => _useAlphaPerChannel;
      set {
        _useAlphaPerChannel = value;
        PortsChanged();
      }
    }

    protected override void Definition() {
      base.Definition();

      if (_useCustomColorBasis) {
        ColorBasisR = ValueInput<DValue>(nameof(ColorBasisR), Color.red);
        ColorBasisG = ValueInput<DValue>(nameof(ColorBasisG), Color.green);
        ColorBasisB = ValueInput<DValue>(nameof(ColorBasisB), Color.blue);
      }
      HueShift = ValueInput<DValue>(nameof(HueShift), 0.0f);
      ShiftR = ValueInput<DValue>(nameof(ShiftR), Vector2.zero);
      ShiftG = ValueInput<DValue>(nameof(ShiftG), Vector2.zero);
      ShiftB = ValueInput<DValue>(nameof(ShiftB), Vector2.zero);
      if (_useAlphaPerChannel) {
        AlphaR = ValueInput<DValue>(nameof(AlphaR), 1.0f);
        AlphaG = ValueInput<DValue>(nameof(AlphaG), 1.0f);
        AlphaB = ValueInput<DValue>(nameof(AlphaB), 1.0f);
      }
    }

    protected override string ShaderPath => "Hidden/TexColorOffset";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetColor(_ColorBasisR, _useCustomColorBasis ? flow.GetValue<DValue>(ColorBasisR) : Color.red);
      material.SetColor(_ColorBasisG, _useCustomColorBasis ? flow.GetValue<DValue>(ColorBasisG) : Color.green);
      material.SetColor(_ColorBasisB, _useCustomColorBasis ? flow.GetValue<DValue>(ColorBasisB) : Color.blue);
      material.SetFloat(_HueShift, flow.GetValue<DValue>(HueShift) / 360.0f * Mathf.PI * 2);
      material.SetVector(_ShiftR, (Vector2)flow.GetValue<DValue>(ShiftR));
      material.SetVector(_ShiftG, (Vector2)flow.GetValue<DValue>(ShiftG));
      material.SetVector(_ShiftB, (Vector2)flow.GetValue<DValue>(ShiftB));
      material.SetFloat(_AlphaR, _useAlphaPerChannel ? flow.GetValue<DValue>(AlphaR) : 1.0f);
      material.SetFloat(_AlphaG, _useAlphaPerChannel ? flow.GetValue<DValue>(AlphaG) : 1.0f);
      material.SetFloat(_AlphaB, _useAlphaPerChannel ? flow.GetValue<DValue>(AlphaB) : 1.0f);
    }
  }
}
