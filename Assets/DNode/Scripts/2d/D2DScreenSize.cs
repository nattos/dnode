using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D2DScreenSize : Unit {
    [DoNotSerialize] public ValueOutput resultSize;
    [DoNotSerialize] public ValueOutput resultRatio;

    protected override void Definition() {
      resultSize = ValueOutput<Vector2>("Size", DNodeUtils.CachePerFrame(flow => {
        var camera = DScriptMachine.CurrentInstance.GlobalCamera;
        return new Vector2(camera.pixelWidth, camera.pixelHeight);
      }));
      resultRatio = ValueOutput<double>("Ratio", DNodeUtils.CachePerFrame(flow => {
        var camera = DScriptMachine.CurrentInstance.GlobalCamera;
        return camera.pixelWidth / (double)camera.pixelHeight;
      }));
    }
  }
}
