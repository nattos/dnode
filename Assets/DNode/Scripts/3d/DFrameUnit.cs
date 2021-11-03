using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public abstract class DFrameUnit : Unit {
    protected override void Definition() {}

    protected Transport Transport => DScriptMachine.CurrentInstance.Transport;
    protected PrefabCache PrefabCache => DScriptMachine.CurrentInstance.PrefabCache;
    protected MeshGlyphCache MeshGlyphCache => DScriptMachine.CurrentInstance.MeshGlyphCache;

    public static DValue? GetNullableDValueFromDEventInput(Flow flow, ValueInput input, bool hasDefault = true) {
      if (!hasDefault && !input.connections.Any()) {
        return null;
      }
      DEvent devent = flow.GetValue<DEvent>(input);
      if (!devent.IsTriggered) {
        return null;
      }
      return devent.Value;
    }

    public static DFrameTexture GetNullableDFrameTexture(Flow flow, ValueInput input) {
      if (!input.connections.Any()) {
        return null;
      }
      return flow.GetValue<DFrameTexture>(input);
    }
  
    public static T? GetNullableValue<T>(Flow flow, ValueInput input) where T : struct {
      if (!input.connections.Any()) {
        return default;
      }
      return flow.GetValue<T>(input);
    }

    public static DFrameArray<DFrameObject> GetFrameObjects(Flow flow, ValueInput input) {
      if (!input.connections.Any()) {
        return default;
      }
      return flow.GetValue<DFrameArray<DFrameObject>>(input);
    }
  }
}
