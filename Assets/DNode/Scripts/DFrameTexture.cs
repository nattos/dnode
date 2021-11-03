using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  [TypeIcon(typeof(Texture))]
  public class DFrameTexture : DFrameNode {
    public Texture Texture;

    public static implicit operator DFrameTexture(Texture value) {
      return new DFrameTexture { Texture = value };
    }

    public static implicit operator Texture(DFrameTexture value) {
      return value?.Texture;
    }

    public override string ToShortString() => Texture == null ? "Null" : Unity.VisualScripting.StringUtility.Truncate(Texture.name, 20);
  }
}
