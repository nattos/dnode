using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DSceneCamera : DFrameUnit {
    [DoNotSerialize] public ValueOutput resultPosition;
    [DoNotSerialize] public ValueOutput resultRotation;

    protected override void Definition() {
      resultPosition = ValueOutput<Vector3>("Position", DNodeUtils.CachePerFrame(flow => DScriptMachine.CurrentInstance.SceneCamera.OrNull()?.transform?.position ?? Vector3.zero));
      resultRotation = ValueOutput<Vector3>("Rotation", DNodeUtils.CachePerFrame(flow => DScriptMachine.CurrentInstance.SceneCamera.OrNull()?.transform?.rotation.eulerAngles ?? Vector3.zero));
    }
  }
}
