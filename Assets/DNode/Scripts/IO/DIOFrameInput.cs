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

    private DIOFrameIOTechnique _currentTechnique;
    private IFrameReceiver _receiver;
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
      Texture texture = _receiver?.ReceivedTexture?.OrNull();
      Vector2Int? size = texture == null ? null : (Vector2Int?)new Vector2Int(texture?.width ?? 0, texture?.height ?? 0);
      return new DEnvironmentOverrides {
        OutputSize = useAsOutputSize ? size : (Vector2Int?)null,
        InputSize = useAsInputSize ? size : (Vector2Int?)null,
      };
    }

    private void StartReceiever(DIOFrameIOTechnique technique) {
      if (_receiver?.IsAlive == true && _currentTechnique == technique) {
        return;
      }
      StopReceiver();
      _currentTechnique = technique;
      _receiver = FrameReceivers.CreateReceiever(technique);
      _receiver.StartReceiver();
    }

    private void StopReceiver() {
      if (_receiver?.IsAlive != true) {
        return;
      }
      var receiver = _receiver;
      DScriptMachine.DelayCall(() => {
        receiver.Dispose();
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
        StartReceiever(source);
        _receiver.RemoteName = address;
        if (flow.GetValue<bool>(Bypass)) {
          return UnityUtils.BlankTexture;
        }
        return _receiver.ReceivedTexture.OrNull() ?? (Texture)UnityUtils.BlankTexture;
      }
      result = ValueOutput<DFrameTexture>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
