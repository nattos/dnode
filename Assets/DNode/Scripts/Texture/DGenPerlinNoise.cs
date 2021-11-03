using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DGenPerlinNoise : DTexGenBlitUnit {
    private int _Octaves = Shader.PropertyToID("_Octaves");
    private int _Gain = Shader.PropertyToID("_Gain");
    private int _Scale = Shader.PropertyToID("_Scale");
    private int _Frequency = Shader.PropertyToID("_Frequency");
    private int _Amplitude = Shader.PropertyToID("_Amplitude");
    private int _Stretch = Shader.PropertyToID("_Stretch");
    private int _Phase = Shader.PropertyToID("_Phase");

    [DoNotSerialize][PortLabelHidden][Integer][Range(1, 16, 8)][ShortEditor] public ValueInput Octaves;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 1)][ShortEditor] public ValueInput Gain;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 32, 8)][ShortEditor] public ValueInput Scale;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 2)][ShortEditor] public ValueInput Frequency;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 1, 0.8)][ShortEditor] public ValueInput Amplitude;
    [DoNotSerialize][PortLabelHidden][Vector2][ScaleRange][ShortEditor] public ValueInput Stretch;
    [DoNotSerialize][PortLabelHidden][Vector3][ZeroOneRange][ClampMode(ClampMode.Wrap)][ShortEditor] public ValueInput Phase;

    protected override void Definition() {
      base.Definition();

      Octaves = ValueInput<DValue>(nameof(Octaves), 8);
      Gain = ValueInput<DValue>(nameof(Gain), 1.0f);
      Scale = ValueInput<DValue>(nameof(Scale), 8.0f);
      Frequency = ValueInput<DValue>(nameof(Frequency), 2.0f);
      Amplitude = ValueInput<DValue>(nameof(Amplitude), 0.8f);
      Stretch = ValueInput<DValue>(nameof(Stretch), Vector2.one);
      Phase = ValueInput<DValue>(nameof(Phase), Vector3.zero);
    }

    protected override string ShaderPath => "Hidden/GenPerlin";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      material.SetInt(_Octaves, (int)Math.Round((double)flow.GetValue<DValue>(Octaves)));
      material.SetFloat(_Gain, (float)flow.GetValue<DValue>(Gain));
      material.SetFloat(_Scale, (float)flow.GetValue<DValue>(Scale));
      material.SetFloat(_Frequency, (float)flow.GetValue<DValue>(Frequency));
      material.SetFloat(_Amplitude, (float)flow.GetValue<DValue>(Amplitude));
      material.SetVector(_Stretch, Vector2.one.ElementDiv(flow.GetValue<DValue>(Stretch)));
      material.SetVector(_Phase, (Vector3)flow.GetValue<DValue>(Phase));
    }
  }
}
