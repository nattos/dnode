using Unity.VisualScripting;

namespace DNode {
  public class DDelay : Unit {
    [DoNotSerialize]
    public ValueInput Input;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    private DValue _cachedValue;
    private DValue _nextValue;
    private int _cachedFrame = -1;

    protected override void Definition() {
      Input = ValueInput<DValue>("Input");

      DValue ComputeFromFlow(Flow flow) {
        int currentFrame = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
        if (currentFrame != _cachedFrame) {
          _cachedValue = _nextValue;
          _cachedFrame = currentFrame;
          _nextValue = flow.GetValue<DValue>(Input);
        }
        return _cachedValue;
      }

      result = ValueOutput<DValue>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
