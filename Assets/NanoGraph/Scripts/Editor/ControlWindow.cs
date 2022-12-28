using NanoGraph.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NanoGraph {
  public class ControlWindow : EditorWindow {
    private List<TestWindow.SelectedInput> SelectedInputs => TestWindow.Instance?.SelectedInputs;

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
      int inputCount = PluginService.Instance.TextureInputCount;
      if (SelectedInputs == null) {
        return;
      }
      while (SelectedInputs.Count < inputCount) {
        SelectedInputs.Add(default);
      }

      (string serverName, string appName)[] serverList = Klak.Syphon.SyphonClientService.ServerList;
      string[] serverListStrings = serverList.Select(pair => $"{pair.serverName} {pair.appName}").Append("None").ToArray();
      for (int i = 0; i < inputCount; ++i) {
        var oldSelectedInput = SelectedInputs[i];
        int oldInputIndex = System.Array.IndexOf(serverList, (oldSelectedInput.ServerName, oldSelectedInput.AppName));
        int newInputIndex = EditorGUILayout.Popup(oldInputIndex, serverListStrings);
        var newSelectedInput = (newInputIndex >= 0 && newInputIndex < serverList.Length) ? serverList[newInputIndex] : default;
        SelectedInputs[i] = new TestWindow.SelectedInput { ServerName = newSelectedInput.serverName, AppName = newSelectedInput.appName };
      }

      foreach (var parameter in PluginService.Instance.GetParameters()) {
        using (var check = new EditorGUI.ChangeCheckScope()) {
          float newValue = EditorGUILayout.Slider(parameter.Name, (float)parameter.Value, (float)parameter.MinValue, (float)parameter.MaxValue);
          if (check.changed) {
            PluginService.Instance.SetParameter(parameter.Name, newValue);
          }
        }
      }
    }

    [MenuItem("Do/Show Control Window")]
    public static void ShowWindow() {
      EditorWindow.GetWindow<ControlWindow>("Control").Show();
    }
  }
}
