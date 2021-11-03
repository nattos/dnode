using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DNode {
  [VolumeComponentMenu("Sky/BackgroundTexture")]
  [SkyUniqueID(UniqueId)]
  public class BackgroundTextureSkySettings : SkySettings {
    public const int UniqueId = unchecked((int)0x9E548D0E); // Truncated MD5 hash of "BackgroundTextureSkySettings" (9E548D0E3AE9665B4C161C75D7539940)

    public TextureParameter Texture = new TextureParameter(null);
    public ClampedFloatParameter TextureAlpha = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

    [Tooltip("Specifies the color of the upper hemisphere of the sky.")]
    public ColorParameter top = new ColorParameter(Color.blue, true, false, true);
    [Tooltip("Specifies the color at the horizon.")]
    public ColorParameter middle = new ColorParameter(new Color(0.3f, 0.7f, 1f), true, false, true);
    [Tooltip("Specifies the color of the lower hemisphere of the sky. This is below the horizon.")]
    public ColorParameter bottom = new ColorParameter(Color.white, true, false, true);
    [Tooltip("Sets the size of the horizon (Middle color).")]
    public FloatParameter gradientDiffusion = new FloatParameter(1);

    public override int GetHashCode() {
      int hash = base.GetHashCode();

      unchecked {
        hash = hash * 23 + bottom.GetHashCode();
        hash = hash * 23 + top.GetHashCode();
        hash = hash * 23 + middle.GetHashCode();
        hash = hash * 23 + gradientDiffusion.GetHashCode();
        hash = hash * 23 + Texture.GetHashCode();
        hash = hash * 23 + TextureAlpha.GetHashCode();
      }

      return hash;
    }

    public override Type GetSkyRendererType() { return typeof(BackgroundTextureSkyRenderer); }
  }
}
