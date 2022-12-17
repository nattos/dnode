using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NanoGraph.Plugin {
  public class TestComponent : MonoBehaviour {
    public RenderTexture RT;
    public float phase = 0.0f;
    public bool ShowInput;

    // public void Update() {
    //   var server = GetComponent<Klak.Syphon.SyphonServer>();
    //   var texture = PluginService.Instance.GetTextureInput();
    //   // Graphics.CopyTexture(server._serverTexture, texture);

    //   var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.BGRA32);
    //   Graphics.CopyTexture(rt, texture);
    //   RenderTexture.ReleaseTemporary(rt);
    //   // Graphics.CopyTexture(Texture2D.redTexture, 0, 0, 0, 0, Texture2D.redTexture.width, Texture2D.redTexture.height, texture, 0, 0, 100, 100);
    // }
    void OnRenderImage(RenderTexture source, RenderTexture dest) {
      var server = GetComponent<Klak.Syphon.SyphonServer>();
      Texture2D _serverTexture = PluginService.Instance.GetTextureInput();
      if (_serverTexture != null && server._blitMaterial != null) {
        // Capture the camera render.
        var temp = RenderTexture.GetTemporary(
            _serverTexture.width, _serverTexture.height, 0,
            RenderTextureFormat.Default, RenderTextureReadWrite.Default
        );
        Graphics.Blit(source, temp, server._blitMaterial, server._alphaSupport ? 1 : 0);
        RenderTexture oldRT = RenderTexture.active;
        RenderTexture.active = temp;
        GL.Begin(GL.LINES);
        GL.Vertex3(0.0f, 0.0f, 0.0f);
        GL.Vertex3(10.0f, 10.0f, 10.0f);
        GL.Color(Color.red);
        GL.End();
        RenderTexture.active = oldRT;
        Graphics.CopyTexture(temp, _serverTexture);
        RenderTexture.ReleaseTemporary(temp);
      }

      // Dumb blit
      Graphics.Blit(source, dest);
    }

    // public void Update() {
    //   if (RT == null) {
    //     RT = new RenderTexture(1920, 1080, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
    //   }

    //   // RenderTexture rt = RT;
    //   // RenderTexture.active = rt;
    //   // phase += 0.001f;
    //   // GL.Clear(clearDepth: false, clearColor: true, new Color(Mathf.Abs(Mathf.Sin(phase)), 0.0f, 0.0f, 1.0f));
    //   // RenderTexture.active = null;
    //   Texture2D texture;
    //   if (ShowInput) {
    //     texture = PluginService.Instance.GetTextureInput();
    //   } else {
    //     texture = PluginService.Instance.GetTextureOutput();
    //   }
    //   Graphics.CopyTexture(texture, RT);

    //   var server = GetComponent<Klak.Syphon.SyphonServer>();
    //   server.sourceTexture = RT;
    // }
  }
}
