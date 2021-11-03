using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public enum TextureSizeSource {
    Source,
    EnvOutput,
    EnvInput,
  }

  public class RenderTextureCache : IFrameComponent {
    private struct Key {
      public int Width;
      public int Height;
    }

    public int ScreenWidth = 1920;
    public int ScreenHeight = 1080;

    public int _allocatedCount = 0;
    private Dictionary<Key, List<RenderTexture>> _allocated = new Dictionary<Key, List<RenderTexture>>();
    private Dictionary<Key, List<RenderTexture>> _free = new Dictionary<Key, List<RenderTexture>>();

    public int AllocatedCount => _allocatedCount;

    public void Dispose() {
      ReleaseAll();
    }

    public void OnStartFrame() {
      ReleaseAll();
    }

    private void ReleaseAll() {
      foreach (var entry in _allocated) {
        foreach (RenderTexture texture in entry.Value) {
          RenderTexture.ReleaseTemporary(texture);
        }
      }
      _allocatedCount = 0;
      _allocated.Clear();
      _free.Clear();
    }

    public void OnEndFrame() {
      //Debug.Log($"Render textures alive: {_allocated.Count}");
    }

    public static Vector2Int GetSizeFromSource(Vector2Int? sourceSize, TextureSizeSource sizeSource) {
      return GetSizeFromSource(sourceSize ?? DScriptMachine.CurrentInstance.Environment.EffectiveOutputSize, sizeSource);
    }

    public static Vector2Int GetSizeFromSource(Vector2Int sourceSize, TextureSizeSource sizeSource) {
      switch (sizeSource) {
        default:
        case TextureSizeSource.Source:
          return sourceSize;
        case TextureSizeSource.EnvOutput:
          return DScriptMachine.CurrentInstance.Environment.EffectiveOutputSize;
        case TextureSizeSource.EnvInput:
          return DScriptMachine.CurrentInstance.Environment.EffectiveInputSize;
      }
    }

    public static Vector2Int GetSizeFromSource(Texture sourceTexture, TextureSizeSource sizeSource) {
      Vector2Int? sourceSize = sourceTexture ? (Vector2Int?)new Vector2Int(sourceTexture.width, sourceTexture.height) : null;
      return GetSizeFromSource(sourceSize, sizeSource);
    }

    public RenderTexture Allocate(Texture sourceTexture, TextureSizeSource sizeSource) {
      return Allocate(GetSizeFromSource(sourceTexture, sizeSource));
    }

    public RenderTexture Allocate(Vector2Int size) {
      return Allocate(size.x, size.y);
    }

    public RenderTexture Allocate(int width, int height) {
      Key key = new Key { Width = width, Height = height };
      if (!_allocated.TryGetValue(key, out var allocated)) {
        allocated = new List<RenderTexture>();
        _allocated[key] = allocated;
      }
      if (!_free.TryGetValue(key, out var free)) {
        free = new List<RenderTexture>();
        _free[key] = free;
      }

      if (free.Count > 0) {
        RenderTexture freeTexture = free[free.Count - 1];
        free.RemoveAt(free.Count - 1);
        return freeTexture;
      }
      RenderTexture texture = RenderTexture.GetTemporary(width, height, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat);
      allocated.Add(texture);
      _allocatedCount++;
      return texture;
    }

    public void Free(RenderTexture texture) {
      Key key = new Key { Width = texture.width, Height = texture.height };
      _free[key].Add(texture);
    }
  }
}
