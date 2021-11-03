using UnityEngine;

namespace DNode {
  public static class D2DUtils {
    public static Vector2 WorldToScreen(Vector3 world) {
      return WorldToScreenAndDepth(world).screen;
    }

    public static (Vector2 screen, float depth) WorldToScreenAndDepth(Vector3 world) {
      var camera = DScriptMachine.CurrentInstance.GlobalCamera;
      Vector2 pixelHalfSize = new Vector2(camera.pixelWidth, camera.pixelHeight) * 0.5f;
      Vector3 screen = camera.WorldToScreenPoint(world);
      return (new Vector2(screen.x, screen.y) - pixelHalfSize, screen.z);
    }

    public static Vector3 ScreenToWorld(Vector2 screen, float depth) {
      var camera = DScriptMachine.CurrentInstance.GlobalCamera;
      Vector2 pixelHalfSize = new Vector2(camera.pixelWidth, camera.pixelHeight) * 0.5f;
      screen += pixelHalfSize;
      return camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
    }
  }
}
