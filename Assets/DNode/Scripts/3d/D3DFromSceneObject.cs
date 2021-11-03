using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DFromSceneObject : DFrameUnit {
    [DoNotSerialize][PortKey("GameObject")] public ValueInput GameObjectRef;
    [DoNotSerialize] public ValueInput ByName;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      GameObjectRef = ValueInput<GameObject>("GameObject", null);
      ByName = ValueInput<string>(nameof(ByName), "");

      DFrameArray<DFrameObject> ComputeFromFlow(Flow flow) {
        GameObject instance = flow.GetValue<GameObject>(GameObjectRef);
        if (!instance) {
          string name = flow.GetValue<string>(ByName);
          instance = GameObject.Find(name);
        }
        if (instance) {
          foreach (var frameComponent in instance.GetComponentsInChildren<FrameComponentBase>()) {
            frameComponent.ResetToInitialValues();
          }
        }
        DMutableFrameArray<DFrameObject> result = new DMutableFrameArray<DFrameObject>(1);
        result[0] = new DFrameObject { GameObject = instance };
        return result.ToValue();
      }

      result = ValueOutput<DFrameArray<DFrameObject>>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
