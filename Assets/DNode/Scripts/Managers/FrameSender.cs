using System;
using UnityEngine;

namespace DNode {
  public interface IFrameSender : IDisposable {
    string Name { get; set; }
    RenderTexture TextureToSend { get; set; }
    bool UseAlphaChannel { get; set; }
    bool IsAlive { get; }

    void StartSender();
    void StopSender();
  }

  public static class FrameSenders {
    public static IFrameSender CreateSender(DIOFrameIOTechnique technique) {
      switch (technique) {
        case DIOFrameIOTechnique.Spout:
          return new SpoutFrameSender();
        case DIOFrameIOTechnique.Syphon:
          return new SyphonFrameSender();
        default:
        case DIOFrameIOTechnique.DefaultLocal:
        #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
          return new SyphonFrameSender();
        #else
          return new SpoutFrameSender();
        #endif
      }
    }
  }


  public class SpoutFrameSender : IFrameSender {
    private static Klak.Spout.SpoutResources _spoutResources;
    private Klak.Spout.SpoutSender _sender;

    public string Name {
      get => _sender?.spoutName;
      set {
        if (!_sender) {
          return;
        }
        _sender.spoutName = value;
      }
    }

    private RenderTexture _textureToSend;
    public RenderTexture TextureToSend {
      get => _textureToSend;
      set {
        if (_sender) {
          _textureToSend = value;
          _sender.sourceTexture = value;
        }
      }
    }
    public bool UseAlphaChannel {
      get => _sender.OrNull()?.keepAlpha ?? false;
      set {
        if (_sender) {
          _sender.keepAlpha = value;
        }
      }
    }

    public bool IsAlive => _sender != null;

    public void Dispose() {
      StopSender();
    }

    public void StartSender() {
      if (_sender) {
        return;
      }
      if (!_spoutResources) {
        _spoutResources = ScriptableObject.CreateInstance<Klak.Spout.SpoutResources>();
        _spoutResources.blitShader = Shader.Find("Hidden/Klak/Spout/Blit");
      }
      var gameObject = new GameObject(nameof(DIOFrameInput), typeof(Klak.Spout.SpoutSender));
      _sender = gameObject.GetComponent<Klak.Spout.SpoutSender>();
      _sender.SetResources(_spoutResources);
      _sender.captureMethod = Klak.Spout.CaptureMethod.Texture;
    }

    public void StopSender() {
      if (!_sender) {
        return;
      }
      UnityUtils.Destroy(_sender.gameObject);
      _sender = null;
    }
  }

  public class SyphonFrameSender : IFrameSender {
    private Klak.Syphon.SyphonServer _sender;

    public string Name {
      get => _sender?.Name;
      set {
        if (!_sender) {
          return;
        }
        _sender.Name = value;
      }
    }

    public RenderTexture TextureToSend {
      get => _sender.OrNull()?.sourceTexture;
      set {
        if (_sender) {
          _sender.sourceTexture = value;
        }
      }
    }

    public bool UseAlphaChannel {
      get => _sender.OrNull()?.alphaSupport ?? false;
      set {
        if (_sender) {
          _sender.alphaSupport = value;
        }
      }
    }

    public bool IsAlive => _sender != null;

    private (int width, int height, bool alpha, string name) _cacheKey;

    public void Dispose() {
      StopSender();
    }

    public void StartSender() {
      var cacheKey = (TextureToSend.OrNull()?.width ?? 0, TextureToSend.OrNull()?.height ?? 0, UseAlphaChannel, Name);
      if (_sender && cacheKey == _cacheKey) {
        return;
      }
      StopSender();
      _cacheKey = cacheKey;
      var gameObject = new GameObject(nameof(DIOFrameInput), typeof(Klak.Syphon.SyphonServer));
      _sender = gameObject.GetComponent<Klak.Syphon.SyphonServer>();
    }

    public void StopSender() {
      if (!_sender) {
        return;
      }
      UnityUtils.Destroy(_sender.gameObject);
      _sender = null;
    }
  }
}
