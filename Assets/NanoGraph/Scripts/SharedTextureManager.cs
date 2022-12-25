using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NanoGraph {
  public class SharedTexture : IDisposable {
    public readonly SharedTextureManager Parent;
    public readonly Texture2D Texture;
    public readonly Int32 IOSurfaceID;

    public SharedTexture(SharedTextureManager parent, Texture2D texture, Int32 ioSurfaceID) {
      this.Parent = parent;
      this.Texture = texture;
      this.IOSurfaceID = ioSurfaceID;
    }

    public void Dispose() {
      Parent.DestroyTexture(this);
    }
  }

  public class SharedTextureManager : ScriptableObject {
    private readonly Dictionary<SharedTexture, IntPtr> _textures = new Dictionary<SharedTexture, IntPtr>();

    public SharedTexture CreateTexture(int width, int height) {
      IntPtr handle = Plugin_CreateSharedTexture(width, height);
      IntPtr nativeHandle = Plugin_GetSharedTextureTexture(handle);
      Texture2D rawTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.BGRA32, mipChain: false, linear: false, nativeHandle);
      SharedTexture texture = new SharedTexture(this, rawTexture, Plugin_GetSharedTextureSurfaceID(handle));
      _textures[texture] = handle;
      return texture;
    }

    public SharedTexture CreateTextureFromSurfaceId(Int32 surfaceId) {
      IntPtr handle = Plugin_CreateSharedTextureFromSurfaceId(surfaceId);
      IntPtr nativeHandle = Plugin_GetSharedTextureTexture(handle);
      int width = Plugin_GetSharedTextureWidth(handle);
      int height = Plugin_GetSharedTextureHeight(handle);
      Texture2D rawTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.BGRA32, mipChain: false, linear: false, nativeHandle);
      SharedTexture texture = new SharedTexture(this, rawTexture, Plugin_GetSharedTextureSurfaceID(handle));
      _textures[texture] = handle;
      return texture;
    }

    public void DestroyTexture(SharedTexture texture) {
      if (_textures.TryGetValue(texture, out IntPtr handle)) {
        _textures.Remove(texture);
        Plugin_DestroyTexture(handle);
      }
      if (texture.Texture) {
        DestroyImmediate(texture.Texture);
      }
    }

    public void OnDisable() {
      foreach (IntPtr handle in _textures.Values) {
        Plugin_DestroyTexture(handle);
      }
      _textures.Clear();
    }

    [DllImport("MetalPlugin")]
    private static extern IntPtr Plugin_CreateSharedTexture(Int32 width, Int32 height);
    [DllImport("MetalPlugin")]
    private static extern IntPtr Plugin_CreateSharedTextureFromSurfaceId(Int32 surfaceId);
    [DllImport("MetalPlugin")]
    private static extern Int32 Plugin_GetSharedTextureWidth(IntPtr texture);
    [DllImport("MetalPlugin")]
    private static extern Int32 Plugin_GetSharedTextureHeight(IntPtr texture);
    [DllImport("MetalPlugin")]
    private static extern Int32 Plugin_GetSharedTextureSurfaceID(IntPtr texture);
    [DllImport("MetalPlugin")]
    private static extern IntPtr Plugin_GetSharedTextureTexture(IntPtr texture);
    [DllImport("MetalPlugin")]
    private static extern void Plugin_DestroyTexture(IntPtr texture);

    private static SharedTextureManager _cachedInstance;

    public static SharedTextureManager Instance {
      get {
        if (_cachedInstance) {
          return _cachedInstance;
        }
        SharedTextureManager instance = Resources.FindObjectsOfTypeAll(typeof(SharedTextureManager)).FirstOrDefault() as SharedTextureManager;
        if (!instance) {
          instance = ScriptableObject.CreateInstance<SharedTextureManager>();
        }
        _cachedInstance = instance;
        return instance;
      }
    }
  }
}
