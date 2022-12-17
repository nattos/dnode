using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace NanoGraph.Plugin {
  public class TestWindow : EditorWindow {
    public bool Input;

    public void OnEnable() {
      PluginService.Instance.TextureOutputsUpdated += OnTextureOutputsUpdated;
    }

    public void OnDisable() {
      PluginService.Instance.TextureOutputsUpdated -= OnTextureOutputsUpdated;
    }

    private void OnTextureOutputsUpdated() {
      Repaint();
    }

    public void OnGUI() {
      if (GUILayout.Button("Restart Plugin")) {
        EditorApplication.delayCall += () => {
          PluginService.Instance.StopRendering();
          PluginService.Instance.StartRendering();
        };
      }
      Input = EditorGUILayout.ToggleLeft("Show Input", Input);
      Rect textureRect = EditorGUILayout.GetControlRect(GUILayout.Height(400));
      Texture2D texture = Input ? PluginService.Instance.GetTextureInput() : PluginService.Instance.GetTextureOutput();
      if (!texture) {
        texture = Texture2D.blackTexture;
      }
      EditorGUI.DrawPreviewTexture(textureRect, texture, null, ScaleMode.ScaleToFit);

      foreach (var parameter in PluginService.Instance.GetParameters()) {
        using (var check = new EditorGUI.ChangeCheckScope()) {
          float newValue = EditorGUILayout.Slider(parameter.Name, (float)parameter.Value, (float)parameter.MinValue, (float)parameter.MaxValue);
          if (check.changed) {
            PluginService.Instance.SetParameter(parameter.Name, newValue);
          }
        }
      }
    }


    [MenuItem("Do/Show Test Window")]
    public static void ShowWindow() {
      EditorWindow.GetWindow<TestWindow>().Show();
    }
  }
}
