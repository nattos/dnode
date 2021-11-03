using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexPosterize : DTexUnaryPreserveAlphaBlitUnit {
    private int _Steps = Shader.PropertyToID("_Steps");

    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 4)][ShortEditor] public ValueInput Steps;
    [DoNotSerialize][PortLabelHidden][Vector4][Range(0, 16, 4)] public ValueInput ChannelSteps;

    private bool _useChannels = false;
    [Serialize][Inspectable] public bool UseChannels {
      get => _useChannels;
      set {
        _useChannels = value;
        PortsChanged();
      }
    }

    protected override void Definition() {
      base.Definition();
      if (_useChannels) {
        ChannelSteps = ValueInput<DValue>(nameof(ChannelSteps), Vector4.one * 4.0f);
      } else {
        Steps = ValueInput<DValue>(nameof(Steps), 4.0);
      }
    }

    protected override string ShaderPath => "Hidden/TexPosterize";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);
      if (_useChannels) {
        material.SetVector(_Steps, flow.GetValue<DValue>(ChannelSteps));
      } else {
        float value = flow.GetValue<DValue>(Steps);
        material.SetVector(_Steps, new Vector4(value, value, value, value));
      }
    }
  }
}
