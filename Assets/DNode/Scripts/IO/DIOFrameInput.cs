using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DIOFrameInput : Unit {
    [DoNotSerialize] public ValueInput Source;
    [DoNotSerialize] public ValueInput Bypass;
    [DoNotSerialize] public ValueInput Address;
    [DoNotSerialize] public ValueInput UseAsInputSize;
    [DoNotSerialize] public ValueInput UseAsOutputSize;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private static Klak.Spout.SpoutResources _spoutResources;
    private Klak.Spout.SpoutReceiver _receiver;
    private DEnvironmentOverrideProviderHandle _environmentOverrideProvider;

    public override void AfterAdd() {
      base.AfterAdd();
      _environmentOverrideProvider = new DEnvironmentOverrideProviderHandle(graph, GetEnvironmentOverrides);
    }

    public override void Dispose() {
      base.Dispose();
      _environmentOverrideProvider.Dispose();
      StopReceiver();
    }

    private DEnvironmentOverrides GetEnvironmentOverrides(Flow flow) {
      bool useAsInputSize = flow.GetValue<bool>(UseAsInputSize);
      bool useAsOutputSize = flow.GetValue<bool>(UseAsOutputSize);
      RenderTexture texture = _receiver.OrNull()?.receivedTexture?.OrNull();
      Vector2Int? size = texture == null ? null : (Vector2Int?)new Vector2Int(texture?.width ?? 0, texture?.height ?? 0);
      return new DEnvironmentOverrides {
        OutputSize = useAsOutputSize ? size : (Vector2Int?)null,
        InputSize = useAsInputSize ? size : (Vector2Int?)null,
      };
    }

    private void StartReceiever() {
      if (_receiver) {
        return;
      }
      if (!_spoutResources) {
        _spoutResources = ScriptableObject.CreateInstance<Klak.Spout.SpoutResources>();
        _spoutResources.blitShader = Shader.Find("Hidden/Klak/Spout/Blit");
      }
      var gameObject = new GameObject(nameof(DIOFrameInput), typeof(Klak.Spout.SpoutReceiver));
      _receiver = gameObject.GetComponent<Klak.Spout.SpoutReceiver>();
      _receiver.SetResources(_spoutResources);
    }

    private void StopReceiver() {
      if (!_receiver) {
        return;
      }
      var receiver = _receiver;
      DScriptMachine.DelayCall(() => {
        UnityUtils.Destroy(receiver.gameObject);
      });
      _receiver = null;
    }

    protected override void Definition() {
      Source = ValueInput<DIOFrameIOTechnique>("Source", DIOFrameIOTechnique.DefaultLocal);
      Bypass = ValueInput<bool>("Bypass", false);
      Address = ValueInput<DIOFrameInputAddressSpec>("Address", "Spout Demo Sender");
      UseAsInputSize = ValueInput<bool>("UseAsInputSize", true);
      UseAsOutputSize = ValueInput<bool>("UseAsOutputSize", false);

      DFrameTexture ComputeFromFlow(Flow flow) {
        DIOFrameIOTechnique source = flow.GetValue<DIOFrameIOTechnique>(Source);
        string address = flow.GetValue<DIOFrameInputAddressSpec>(Address).Address ?? "";
        StartReceiever();
        _receiver.sourceName = address;
        if (flow.GetValue<bool>(Bypass)) {
          return UnityUtils.BlankTexture;
        }
        return _receiver.receivedTexture.OrNull() ?? (Texture)UnityUtils.BlankTexture;
      }
      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
