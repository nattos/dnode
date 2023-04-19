using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;
using System;
using NanoGraph.Plugin;

namespace NanoGraph.VisualScripting {
  [Editor(typeof(FlowGraph))]
  public class NanoGraphEditor : FlowGraphEditor {
    public const float _messagesHeight = 300.0f;

    private static Lazy<Styles> _styles = new Lazy<Styles>(() => new Styles());

    private List<TestWindow.SelectedInput> SelectedInputs => TestWindow.Instance?.SelectedInputs;

    public NanoGraphEditor(Metadata metadata) : base(metadata) {}
    protected override float GetHeight(float width, GUIContent label) {
      float baseHeight = base.GetHeight(width, label);
      float controlsHeight = GetControlsHeight();
      return baseHeight + controlsHeight + _messagesHeight;
    }

    protected override void OnGUI(Rect position, GUIContent label) {      
      base.OnGUI(position, label);
      position.y = y;
      position.height -= y;
      DrawControls(position, out float controlsHeight);
      position.y += controlsHeight;
      position.height -= controlsHeight;
      Rect messagesRect = position;
      messagesRect.y = position.yMax - _messagesHeight;
      messagesRect.height = _messagesHeight;
      DrawMessages(messagesRect);
    }

    private float GetControlsHeight() {
      int inputCount = PluginService.Instance.TextureInputCount;
      float textureInputsHeight = inputCount * EditorGUIUtility.singleLineHeight;

      int parameterCount = PluginService.Instance.GetParameters().Length;
      float parameterHeight = (parameterCount + 1) * EditorGUIUtility.singleLineHeight;

      float targetOutputSizeHeight = 3 * EditorGUIUtility.singleLineHeight;

      float spacing = EditorGUIUtility.singleLineHeight * 2;
      return textureInputsHeight + parameterHeight + targetOutputSizeHeight + spacing;
    }

    private void DrawControls(Rect rect, out float height) {
      height = 0.0f;
      int inputCount = PluginService.Instance.TextureInputCount;
      if (TestWindow.Instance) {
        TestWindow.Instance.SelectedInputs = TestWindow.Instance.SelectedInputs ?? new List<TestWindow.SelectedInput>();
      }
      if (SelectedInputs == null) {
        return;
      }
      while (SelectedInputs.Count < inputCount) {
        SelectedInputs.Add(default);
      }

      rect.y += EditorGUIUtility.singleLineHeight;
      rect.height = EditorGUIUtility.singleLineHeight;

      (string serverName, string appName)[] serverList = Klak.Syphon.SyphonClientService.ServerList;
      string[] serverListStrings = serverList.Select(pair => $"{pair.serverName} {pair.appName}").Append("None").ToArray();
      for (int i = 0; i < inputCount; ++i) {
        var oldSelectedInput = SelectedInputs[i];
        int oldInputIndex = System.Array.IndexOf(serverList, (oldSelectedInput.ServerName, oldSelectedInput.AppName));
        int newInputIndex = EditorGUI.Popup(rect, oldInputIndex, serverListStrings);
        rect.y += rect.height;
        var newSelectedInput = (newInputIndex >= 0 && newInputIndex < serverList.Length) ? serverList[newInputIndex] : default;
        SelectedInputs[i] = new TestWindow.SelectedInput { ServerName = newSelectedInput.serverName, AppName = newSelectedInput.appName };
      }

      foreach (var parameter in PluginService.Instance.GetParameters()) {
        using (var check = new EditorGUI.ChangeCheckScope()) {
          switch (parameter.Type) {
            case NanoValueInputType.Float: {
              float newValue = EditorGUI.Slider(rect, parameter.Name, (float)parameter.Value, (float)parameter.MinValue, (float)parameter.MaxValue);
              if (check.changed) {
                PluginService.Instance.SetParameter(parameter.Name, newValue);
              }
              break;
            }
            case NanoValueInputType.String: {
              string newValue = EditorGUI.TextField(rect, parameter.Name, parameter.StringValue);
              if (check.changed) {
                PluginService.Instance.SetParameterString(parameter.Name, newValue);
              }
              break;
            }
          }
          rect.y += rect.height;
        }
      }
      Rect buttonRect = rect;
      buttonRect.x = buttonRect.xMax - 175;
      buttonRect.width = 175;
      if (GUI.Button(buttonRect, "Reset all to default")) {
        foreach (var parameter in PluginService.Instance.GetParameters()) {
          PluginService.Instance.SetParameter(parameter.Name, parameter.DefaultValue);
          PluginService.Instance.SetParameterString(parameter.Name, parameter.DefaultStringValue);
        }
      }
      rect.y += rect.height;
      rect.y += EditorGUIUtility.singleLineHeight;

      rect.height = EditorGUIUtility.singleLineHeight * 2;
      NanoGraph.DebugInstance.TargetOutputSize = EditorGUI.Vector2IntField(rect, "Target Output Size", NanoGraph.DebugInstance.TargetOutputSize);
      rect.y += rect.height;

      rect.y += EditorGUIUtility.singleLineHeight;
      height = rect.y;
    }

    private void DrawMessages(Rect rect) {
      IEnumerable<string> messages = NanoGraph.DebugInstance?.Messages ?? Array.Empty<string>();
      IReadOnlyList<string> compileErrors = PluginService.Instance.CompileErrors;
      if (compileErrors.Count > 0) {
        messages = messages.Concat(compileErrors);
      }
      EditorGUI.TextArea(rect, string.Join("\n\n", messages) + "\n", _styles.Value.WrappedTextArea);
    }

    private new class Styles {
      public Styles() {
        WrappedTextArea = new GUIStyle(EditorStyles.textArea);
        WrappedTextArea.wordWrap = true;
      }

      public readonly GUIStyle WrappedTextArea;
    }
  }
}
