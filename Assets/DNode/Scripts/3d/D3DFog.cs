using Unity.VisualScripting;

namespace DNode {
  public class D3DFog : DFrameUnit {
    [DoNotSerialize][PortLabelHidden][Boolean] public ValueInput Enabled;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultFarWorldRange, 400)][LogScale] public ValueInput AttenuationDistance;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(-D3DConstants.DefaultFarWorldRange, D3DConstants.DefaultFarWorldRange, 0)] public ValueInput GroundLevel;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, D3DConstants.DefaultFarWorldRange, 400)][LogScale] public ValueInput Height;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Enabled = ValueInput<DEvent>(nameof(Enabled), DEvent.CreateImmediate(1.0, triggered: true));
      AttenuationDistance = ValueInput<DEvent>(nameof(AttenuationDistance), DEvent.CreateImmediate(400.0, triggered: true));
      Height = ValueInput<DEvent>(nameof(Height), DEvent.CreateImmediate(0.0, triggered: true));
      GroundLevel = ValueInput<DEvent>(nameof(GroundLevel), DEvent.CreateImmediate(400.0, triggered: true));
 
      DFrameCommand ComputeFromFlow(Flow flow) {
        var env = DScriptMachine.CurrentInstance.EnvironmentComponent;
        var enabled = flow.GetValue<DEvent>(Enabled);
        if (enabled.IsTriggered) {
          env.FogEnabled.Value = enabled.Value != 0.0;
        }
        var groundLevelEvent = flow.GetValue<DEvent>(GroundLevel);
        var heightEvent = flow.GetValue<DEvent>(Height);
        if (groundLevelEvent.IsTriggered || heightEvent.IsTriggered) {
          float groundLevel = groundLevelEvent.Value;
          float height = heightEvent.Value;
          float heightWeight = 0.25f;
          env.FogBaseHeight.Value = groundLevel - height * heightWeight;
          env.FogMaxHeight.Value = groundLevel + height * (1.0f - heightWeight);
        }

        var attenuationDistance = flow.GetValue<DEvent>(AttenuationDistance);
        if (attenuationDistance.IsTriggered) {
          env.FogAttenuationDistance.Value = attenuationDistance.Value;
        }
        return DFrameCommand.Empty;
      }

      result = ValueOutput<DFrameCommand>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
