using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace DNode {
  [Serializable, VolumeComponentMenu("Post-processing/Custom/BlitTexturePostProcessComponent")]
  public class BlitTexturePostProcessComponent : CustomPostProcessVolumeComponent, IPostProcessComponent {
    public TextureParameter InputTexture = new TextureParameter(null);
    public bool IsActive() => InputTexture.overrideState && InputTexture.value;

    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    public override void Setup() {}

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination) {
      if (InputTexture == null) {
        return;
      }
      Vector4 sizeVec = camera.postProcessScreenSize;
      Vector2 scale = new Vector2(destination.rt.width, destination.rt.height).ElementDiv(new Vector2(sizeVec.x, sizeVec.y));
      cmd.Blit(InputTexture.value, destination, scale, Vector2.zero);
    }

    public override void Cleanup() {}
  }
}
