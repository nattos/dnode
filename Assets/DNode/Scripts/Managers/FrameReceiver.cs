using System;
using UnityEngine;

namespace DNode {
  public interface IFrameReceiver : IDisposable {
    string RemoteName { get; set; }
    Texture ReceivedTexture { get; }
    bool IsAlive { get; }

    void StartReceiver();
    void StopReceiver();
  }

  public static class FrameReceivers {
    public static IFrameReceiver CreateReceiever(DIOFrameIOTechnique technique) {
      switch (technique) {
        case DIOFrameIOTechnique.Spout:
          return new SpoutFrameReceiver();
        case DIOFrameIOTechnique.Syphon:
          return new SyphonFrameReceiver();
        default:
        case DIOFrameIOTechnique.DefaultLocal:
        #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
          return new SyphonFrameReceiver();
        #else
          return new SpoutFrameReceiver();
        #endif
      }
    }
  }


  public class SpoutFrameReceiver : IFrameReceiver {
    private static Klak.Spout.SpoutResources _spoutResources;
    private Klak.Spout.SpoutReceiver _receiver;

    public string RemoteName {
      get => _receiver?.sourceName;
      set {
        if (!_receiver) {
          return;
        }
        _receiver.sourceName = value;
      }
    }

    public Texture ReceivedTexture => _receiver.OrNull()?.receivedTexture;
    public bool IsAlive => _receiver != null;

    public void Dispose() {
      StopReceiver();
    }

    public void StartReceiver() {
      if (_receiver) {
        return;
      }
      if (!_spoutResources) {
        _spoutResources = ScriptableObject.CreateInstance<Klak.Spout.SpoutResources>();
        _spoutResources.blitShader = Shader.Find("Hidden/Klak/Spout/Blit");
      }
      var gameObject = new GameObject(nameof(DIOFrameInput), typeof(Klak.Spout.SpoutReceiver));
      _receiver = gameObject.GetComponent<Klak.Spout.SpoutReceiver>();
      _receiver.SetResources(_spoutResources);
    }

    public void StopReceiver() {
      if (!_receiver) {
        return;
      }
      UnityUtils.Destroy(_receiver.gameObject);
      _receiver = null;
    }
  }

  public class SyphonFrameReceiver : IFrameReceiver {
    private Klak.Syphon.SyphonClient _receiver;

    public string RemoteName {
      get => _receiver?.appName;
      set {
        if (!_receiver) {
          return;
        }
        _receiver.appName = value;
      }
    }

    public Texture ReceivedTexture => _receiver.OrNull()?.receivedTexture;
    public bool IsAlive => _receiver != null;

    public void Dispose() {
      StopReceiver();
    }

    public void StartReceiver() {
      if (_receiver) {
        return;
      }
      var gameObject = new GameObject(nameof(DIOFrameInput), typeof(Klak.Syphon.SyphonClient));
      _receiver = gameObject.GetComponent<Klak.Syphon.SyphonClient>();
    }

    public void StopReceiver() {
      if (!_receiver) {
        return;
      }
      UnityUtils.Destroy(_receiver.gameObject);
      _receiver = null;
    }
  }
}
