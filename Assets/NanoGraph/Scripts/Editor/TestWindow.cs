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
      using (new EditorGUILayout.HorizontalScope()) {
        bool isRendering = PluginService.Instance.IsRendering;
        if (GUILayout.Button(isRendering ? "Stop" : "Start")) {
          EditorApplication.delayCall += () => {
            if (isRendering) {
              PluginService.Instance.StopRendering();
            } else {
              PluginService.Instance.StartRendering();
            }
          };
        }
        Input = EditorGUILayout.ToggleLeft("Show Input", Input);
        EditorGUILayout.Space();
      }
      Texture2D texture = Input ? PluginService.Instance.GetTextureInput() : PluginService.Instance.GetTextureOutput();
      if (!texture) {
        texture = Texture2D.blackTexture;
      }
      float aspectRatio = texture.height / (float) texture.width;
      float desiredHeight = this.position.width * aspectRatio;
      Rect textureRect = EditorGUILayout.GetControlRect(GUILayout.Height(desiredHeight));
      EditorGUI.DrawPreviewTexture(textureRect, texture, null, ScaleMode.ScaleToFit);

      List<string> statusParts = new List<string>();
      bool isCompiling = PluginService.Instance.IsCompiling || (NanoGraph.DebugInstance?.IsCompiling ?? false);
      bool isReloading = PluginService.Instance.IsReloading;
      bool isValidating = (NanoGraph.DebugInstance?.IsValidating ?? false);
      if (isValidating) {
        statusParts.Add("Validate");
      }
      if (isCompiling) {
        statusParts.Add("Compile");
      }
      if (isReloading) {
        statusParts.Add("Reload");
      }
      string statusStr = string.Join(" | ", statusParts);

      Rect statusRect = textureRect;
      statusRect.y = statusRect.yMax - EditorGUIUtility.singleLineHeight;
      statusRect.height = EditorGUIUtility.singleLineHeight;
      EditorGUI.LabelField(statusRect, statusStr);

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
