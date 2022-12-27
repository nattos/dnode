// KlakSyphon - Syphon plugin for Unity
// https://github.com/keijiro/KlakSyphon

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Klak.Syphon {
  public class SyphonClientService : ScriptableObject {
    private class Connection {
      public readonly string AppName;
      public readonly string ServerName;
      public Texture ReceivedTexture => _clientTexture;

      public double LastUpdateTime { get; private set; }

      private IntPtr _clientInstance;
      private Texture _clientTexture;
      private IntPtr _clientNativeTexture;

      public Connection(string serverName, string appName) {
        AppName = appName;
        ServerName = serverName;
        LastUpdateTime = Time.realtimeSinceStartupAsDouble;
      }

      public void Dispose() {
        // Stop the client plugin.
        if (_clientInstance != IntPtr.Zero) {
          Plugin_DestroyClient(_clientInstance);
          _clientInstance = IntPtr.Zero;
        }

        // Dispose the client texture.
        if (_clientTexture != null) {
          if (Application.isPlaying) {
            Destroy(_clientTexture);
          } else {
            DestroyImmediate(_clientTexture);
          }
          _clientTexture = null;
          _clientNativeTexture = IntPtr.Zero;
        }
      }

      public void Update() {
        LastUpdateTime = Time.realtimeSinceStartupAsDouble;
        // If we have no connection yet, keep trying to connect to the server.
        if (_clientInstance == IntPtr.Zero) {
          _clientInstance = Plugin_CreateClient(ServerName, AppName);
        }
        // Break and return if there is no connection at this point.
        if (_clientInstance == IntPtr.Zero) {
          return;
        }
        // If the client has been invalidated, destroy it.
        if (!Plugin_IsClientValid(_clientInstance)) {
          Dispose();
          return;
        }

        // Update the client.
        Plugin_UpdateClient(_clientInstance);

        // Retrieve the native texture pointer from the client.
        var nativeTexture = Plugin_GetClientTexture(_clientInstance);

        // If the texture seems to be changed, release the current texture.
        if (_clientTexture != null &&
            _clientNativeTexture != nativeTexture) {
          if (Application.isPlaying) {
            Destroy(_clientTexture);
          } else {
            DestroyImmediate(_clientTexture);
          }
          _clientTexture = null;
          _clientNativeTexture = IntPtr.Zero;
        }

        // If the client texture is not present, create a new one.
        if (_clientTexture == null && nativeTexture != IntPtr.Zero) {
          _clientTexture = Texture2D.CreateExternalTexture(
              Plugin_GetClientTextureWidth(_clientInstance),
              Plugin_GetClientTextureHeight(_clientInstance),
              TextureFormat.RGBA32, false, false, nativeTexture
          );
          _clientTexture.wrapMode = TextureWrapMode.Clamp;
          _clientNativeTexture = nativeTexture;
        }
      }
    }

    private const double ConnectionTimeoutTime = 1.0;

    [NonSerialized]
    private readonly List<Connection> _connections = new List<Connection>();
    [NonSerialized]
    private readonly Dictionary<(string, string), Connection> _connectionMap = new Dictionary<(string, string), Connection>();
    [NonSerialized]
    private bool _enabled;

    private static readonly List<Connection> _toRemoveConnectionsScratch = new List<Connection>();

    public void OnEnable() {
      _enabled = true;
      SyphonCommon.ApplyCurrentColorSpace();
    }

    public void OnDisable() {
      _enabled = false;
      foreach (Connection connection in _connections) {
        connection.Dispose();
      }
      _connections.Clear();
      _connectionMap.Clear();
    }

    public Texture GetTexture(string serverName, string appName) {
      if (!_enabled) {
        return null;
      }
      var key = (serverName, appName);
      if (!_connectionMap.TryGetValue(key, out Connection connection)) {
        connection = new Connection(serverName, appName);
        _connections.Add(connection);
        _connectionMap[key] = connection;
      }
      // TODO: Rate limit. It's possible that there are multiple readers of the same input.
      connection.Update();
      Texture texture = connection.ReceivedTexture;
      // TODO: These only get sweeped if there's anyone actually reading these. Fix that.
      SweepStaleConnections();
      return texture;
    }

    private void SweepStaleConnections() {
      _toRemoveConnectionsScratch.Clear();
      double currentTime = Time.realtimeSinceStartupAsDouble;
      foreach (Connection connection in _connections) {
        if ((currentTime - connection.LastUpdateTime) > ConnectionTimeoutTime) {
          _toRemoveConnectionsScratch.Add(connection);
        }
      }
      if (_toRemoveConnectionsScratch.Count == 0) {
        return;
      }
      _toRemoveConnectionsScratch.Reverse();
      foreach (Connection toRemove in _toRemoveConnectionsScratch) {
        _connections.Remove(toRemove);
        _connectionMap.Remove((toRemove.ServerName, toRemove.AppName));
      }
      _toRemoveConnectionsScratch.Clear();
    }

    #region Native plugin entry points

    [DllImport("KlakSyphon")]
    static extern IntPtr Plugin_CreateClient(string name, string appName);

    [DllImport("KlakSyphon")]
    static extern void Plugin_DestroyClient(IntPtr instance);

    [DllImport("KlakSyphon")]
    static extern bool Plugin_IsClientValid(IntPtr instance);

    [DllImport("KlakSyphon")]
    static extern IntPtr Plugin_GetClientTexture(IntPtr instance);

    [DllImport("KlakSyphon")]
    static extern int Plugin_GetClientTextureWidth(IntPtr instance);

    [DllImport("KlakSyphon")]
    static extern int Plugin_GetClientTextureHeight(IntPtr instance);

    [DllImport("KlakSyphon")]
    static extern void Plugin_UpdateClient(IntPtr instance);

    public static (string serverName, string appName)[] ServerList {
      get {
        IntPtr list = Plugin_CreateServerList();
        try {
          int count = Plugin_GetServerListCount(list);
          var result = new (string serverName, string appName)[count];
          for (int i = 0; i < count; ++i) {
            result[i] = (PtrToString(Plugin_GetNameFromServerList(list, i)),
                         PtrToString(Plugin_GetAppNameFromServerList(list, i)));
          }
          return result;
        } finally {
          Plugin_DestroyServerList(list);
        }
      }
    }

    private static string PtrToString(IntPtr name, string defaultValue = null) {
      return (name != IntPtr.Zero) ? Marshal.PtrToStringAnsi(name) : defaultValue;
    }

    [DllImport("KlakSyphon")]
    static extern IntPtr Plugin_CreateServerList();

    [DllImport("KlakSyphon")]
    static extern void Plugin_DestroyServerList(IntPtr list);

    [DllImport("KlakSyphon")]
    static extern int Plugin_GetServerListCount(IntPtr list);

    [DllImport("KlakSyphon")]
    static extern IntPtr Plugin_GetNameFromServerList(IntPtr list, int index);

    [DllImport("KlakSyphon")]
    static extern IntPtr Plugin_GetAppNameFromServerList(IntPtr list, int index);

    #endregion

    private static SyphonClientService _cachedInstance;

    public static SyphonClientService Instance {
      get {
        if (_cachedInstance) {
          return _cachedInstance;
        }
        SyphonClientService instance = Resources.FindObjectsOfTypeAll(typeof(SyphonClientService)).FirstOrDefault() as SyphonClientService;
        if (!instance) {
          instance = ScriptableObject.CreateInstance<SyphonClientService>();
        }
        _cachedInstance = instance;
        return instance;
      }
    }
  }
}
