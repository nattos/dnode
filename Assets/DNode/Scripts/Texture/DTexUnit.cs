using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexUnit : DFrameUnit {
    protected void BlitToDebugCaptureTexture(Texture source) {
#if UNITY_EDITOR
      if (DebugCaptureTexture != null) {
        Texture effectiveSource = source.OrNull() ?? Texture2D.blackTexture;
        RenderTexture oldRenderTexture = RenderTexture.active;
        Graphics.Blit(effectiveSource, DebugCaptureTexture.Invoke(new Vector2Int(effectiveSource.width, effectiveSource.height)));
        RenderTexture.active = oldRenderTexture;
      }
#endif // UNITY_EDITOR
    }
    protected void SetCaptureTexturePullHandler(System.Action handler) {
#if UNITY_EDITOR
      DebugCaptureTexturePullHandler = handler;
#endif // UNITY_EDITOR
    }

#if UNITY_EDITOR
    public System.Func<Vector2Int, RenderTexture> DebugCaptureTexture = null;
    public System.Action DebugCaptureTexturePullHandler = null;
#endif // UNITY_EDITOR

    public static Texture GetTextureInput(Flow flow, ValueInput input, Texture defaultTexture = null) {
      Texture texture = null;
      if (input.connections.Any()) {
        texture = flow.GetValue<DFrameTexture>(input);
      }
      if (texture == null) {
        texture = defaultTexture ?? Texture2D.whiteTexture;
      }
      return texture;
    }
  }
}
