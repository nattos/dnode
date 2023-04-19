using NanoGraph.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NanoGraph {
  public class TestWindow : EditorWindow {
    [System.Serializable]
    public struct SelectedInput {
      [SerializeField]
      public string ServerName;
      [SerializeField]
      public string AppName;
    }

    private const float CompileStatusBarHeight = 1.0f;
    private const float CompileStatusSpeed = 1.5f;

    [SerializeField]
    public List<SelectedInput> SelectedInputs;

    public bool Locked;

    private double _lastRenderTime;
    private double _lastRenderFrameTime;
    private IDataNode _selectedOutputNode;

    private float _defaultInputTexturePhase = 0.0f;

    private bool _wasCompiling;
    private double _compileStartTime = 0;
    private int _lastCompileEpoch = 0;

    private readonly Lazy<Styles> _styles = new Lazy<Styles>(() => new Styles());

    [SerializeField]
    public Vector2Int SerializedTargetOutputSize = new Vector2Int(1920, 1080);

    public void OnEnable() {
      Instance = this;
      SelectedInputs = SelectedInputs ?? new List<SelectedInput>();
      _lastRenderTime = Time.realtimeSinceStartupAsDouble;
      _compileStartTime = Time.realtimeSinceStartupAsDouble;
      PluginService.Instance.TextureInputsNeedUpdating += OnTextureInputsNeedUpdating;
      PluginService.Instance.TextureOutputsUpdated += OnTextureOutputsUpdated;

      NanoGraph.DebugInstance.TargetOutputSize = SerializedTargetOutputSize;
    }

    public void OnDisable() {
      if (Instance == this) {
        Instance = null;
      }
      PluginService.Instance.TextureInputsNeedUpdating -= OnTextureInputsNeedUpdating;
      PluginService.Instance.TextureOutputsUpdated -= OnTextureOutputsUpdated;

      SerializedTargetOutputSize = NanoGraph.DebugInstance.TargetOutputSize;
    }

    private void OnTextureInputsNeedUpdating() {
      _defaultInputTexturePhase += 0.05f;

      Texture2D[] inputTextures = PluginService.Instance.GetTextureInputs();
      for (int i = 0; i < inputTextures.Length; ++i) {
        var inputTexture = inputTextures[i];

        Texture sourceTexture = null;
        SelectedInput selectedInput = i < SelectedInputs.Count ? SelectedInputs[i] : default;
        if (!string.IsNullOrEmpty(selectedInput.ServerName) && !string.IsNullOrEmpty(selectedInput.AppName)) {
          sourceTexture = Klak.Syphon.SyphonClientService.Instance.GetTexture(selectedInput.ServerName, selectedInput.AppName);
        }

        if (sourceTexture != null) {
          if (sourceTexture.width == inputTexture.width && sourceTexture.height == inputTexture.height) {
            Graphics.CopyTexture(sourceTexture, inputTexture);
          } else {
            int copyWidth = Mathf.Min(sourceTexture.width, inputTexture.width);
            int copyHeight = Mathf.Min(sourceTexture.height, inputTexture.height);
            int srcX = Mathf.Max(0, (sourceTexture.width - copyWidth) / 2);
            int srcY = Mathf.Max(0, (sourceTexture.height - copyHeight) / 2);
            int dstX = Mathf.Max(0, (inputTexture.width - copyWidth) / 2);
            int dstY = Mathf.Max(0, (inputTexture.height - copyHeight) / 2);
            Graphics.CopyTexture(sourceTexture, 0, 0, srcX, srcY, copyWidth, copyHeight, inputTexture, 0, 0, dstX, dstY);
          }
        } else {
          var temp = RenderTexture.GetTemporary(
              inputTexture.width, inputTexture.height, 0,
              RenderTextureFormat.Default, RenderTextureReadWrite.Default
          );
          RenderTexture oldRT = RenderTexture.active;
          RenderTexture.active = temp;
          GL.Clear(clearDepth: false, clearColor: true, new Color(Mathf.Abs(Mathf.Sin(_defaultInputTexturePhase)), 0.0f, 0.0f, 1.0f));
          RenderTexture.active = oldRT;
          Graphics.CopyTexture(temp, inputTexture);
          RenderTexture.ReleaseTemporary(temp);
        }
      }
    }

    private void OnTextureOutputsUpdated() {
      double currentTime = Time.realtimeSinceStartupAsDouble;
      _lastRenderFrameTime = Time.realtimeSinceStartupAsDouble - _lastRenderTime;
      _lastRenderTime = currentTime;
      Repaint();
    }

    public void Update() {
      PluginService.Instance.PushRenderRequest();
    }

    public void OnGUI() {
      Instance = this;
      Texture2D monitorTexture;
      string monitorDisplayString;
      if (!string.IsNullOrEmpty(PluginService.Instance.DebugOutputTextureKey)) {
        monitorTexture = PluginService.Instance.GetDebugOutputTexture();
        monitorDisplayString = _selectedOutputNode?.ShortName ?? "<unknown node>";
      } else {
        monitorTexture = PluginService.Instance.GetTextureOutput();
        monitorDisplayString = "Output0";
      }
      if (!monitorTexture) {
        monitorTexture = Texture2D.blackTexture;
      }
      // float aspectRatio = monitorTexture.height / (float) monitorTexture.width;
      // float aspectRatio = 9.0f / 16.0f;
      Vector2Int targetOutputSize = NanoGraph.DebugInstance.TargetOutputSize;
      SerializedTargetOutputSize = targetOutputSize;
      PluginService.Instance.RenderSize = targetOutputSize;
      float aspectRatio = targetOutputSize.y / (float)targetOutputSize.x;
      float desiredHeight = this.position.width * aspectRatio;
      Rect textureRect = EditorGUILayout.GetControlRect(GUILayout.Height(desiredHeight));
      EditorGUI.DrawPreviewTexture(textureRect, monitorTexture, null, ScaleMode.ScaleToFit);

      List<string> statusParts = new List<string>();
      statusParts.Add($"{monitorDisplayString} {monitorTexture.width} x {monitorTexture.height}");

      bool isCompiling = PluginService.Instance.IsCompiling || (NanoGraph.DebugInstance?.IsCompiling ?? false);
      bool hasCompileError = PluginService.Instance.HasCompileError;
      bool isReloading = PluginService.Instance.IsReloading;
      bool isValidating = (NanoGraph.DebugInstance?.IsValidating ?? false);
      if (hasCompileError) {
        statusParts.Add("Compile Error");
      }
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
      EditorGUI.LabelField(statusRect, statusStr, _styles.Value.MiniLabel);

      double fpsRate = _lastRenderFrameTime > 0.0 ? (1.0 / _lastRenderFrameTime) : 0.0;
      string fpsStr = $"{fpsRate:F1}";
      Rect fpsRect = textureRect;
      fpsRect.y = fpsRect.yMax - EditorGUIUtility.singleLineHeight;
      fpsRect.height = EditorGUIUtility.singleLineHeight;
      fpsRect.x = fpsRect.xMax - 100.0f;
      fpsRect.width = 100.0f;
      EditorGUI.LabelField(fpsRect, fpsStr, _styles.Value.MiniLabelRight);

      int currentCompileEpoch = PluginService.Instance.CompileEpoch;
      if (_lastCompileEpoch != currentCompileEpoch || _wasCompiling != isCompiling) {
        _compileStartTime = Time.realtimeSinceStartupAsDouble;
        _lastCompileEpoch = currentCompileEpoch;
        _wasCompiling = isCompiling;
      }
      if (isCompiling) {
        Rect compileStatusBarRect = textureRect;
        compileStatusBarRect.y = compileStatusBarRect.yMax - CompileStatusBarHeight;
        compileStatusBarRect.height = CompileStatusBarHeight;
        float compileTime = (float)(Time.realtimeSinceStartupAsDouble - _compileStartTime);
        float compileT = Mathf.Atan(compileTime * CompileStatusSpeed) / Mathf.PI * 2.0f;
        compileStatusBarRect.width *= compileT;
        EditorGUI.DrawRect(compileStatusBarRect, Color.yellow);
      }

      using (new EditorGUILayout.HorizontalScope()) {
        bool isRendering = PluginService.Instance.IsRendering;
        if (ColoredButton(isRendering ? "Stop" : "Start", Color.clear)) {
          EditorApplication.delayCall += () => {
            if (isRendering) {
              PluginService.Instance.StopRendering();
            } else {
              PluginService.Instance.StartRendering();
            }
          };
        }
        if (ColoredButton("▶️", PluginService.Instance.IsPaused ? Color.yellow : Color.clear)) {
          PluginService.Instance.IsPaused = !PluginService.Instance.IsPaused;
        }
        if (ColoredButton("▶️I", Color.clear)) {
          PluginService.Instance.IsPaused = true;
          PluginService.Instance.RequestStepOnce = true;
        }
        if (ColoredButton("Out", Color.clear)) {
          PluginService.Instance.DebugOutputTextureKey = null;
        }
        Locked = EditorGUILayout.ToggleLeft("Locked", Locked, GUILayout.Width(80));
        PluginService.Instance.EnableAutoReload = EditorGUILayout.ToggleLeft("Auto Reload", PluginService.Instance.EnableAutoReload, GUILayout.Width(80));
        PluginService.Instance.EnableDebug = EditorGUILayout.ToggleLeft("Debug", PluginService.Instance.EnableDebug, GUILayout.Width(80));
        if (ColoredButton("Compile", Color.clear)) {
          EditorApplication.delayCall += () => {
            NanoGraph.DebugInstance?.CompileLater();
          };
        }
        if (ColoredButton("Export Plugin", Color.clear)) {
          EditorApplication.delayCall += () => {
            PluginService.Instance.ExportPlugin(NanoGraph.DebugInstance?.EffectName);
          };
        }
        EditorGUILayout.Space(0, expand: true);
      }

      Event e = Event.current;
      if (e.button == 0 && e.clickCount == 2 && textureRect.Contains(e.mousePosition)) {
        this.maximized = !this.maximized;
      }

      UpdateMonitoredNode();
    }

    private void UpdateMonitoredNode() {
      if (Locked) {
        return;
      }
      // Update the active debug node.
      var graphWindow = global::NanoGraph.VisualScripting.GraphWindow.active;
      var context = graphWindow?.context;
      var selection = context?.selection;
      bool hasSelection = selection?.Count > 0;
      global::NanoGraph.VisualScripting.NodeBasedNode selectedBaseNode = selection?.FirstOrDefault() as global::NanoGraph.VisualScripting.NodeBasedNode;
      if (!hasSelection) {
        return;
      }
      string desiredDebugOutputTextureKey;
      if (selectedBaseNode != null) {
        if (_selectedOutputNode == selectedBaseNode.Node) {
          return;
        }
        string nodeDebugId = selectedBaseNode.DebugId ?? "";
        string nodeDebugFieldKey = "";
        foreach (var output in selectedBaseNode.outputs) {
          if (string.IsNullOrEmpty(nodeDebugFieldKey)) {
            nodeDebugFieldKey = output.key;
          }
          if (output.key == "Out") {
            nodeDebugFieldKey = output.key;
            break;
          }
        }
        desiredDebugOutputTextureKey = $"{nodeDebugId}.{nodeDebugFieldKey}";
        _selectedOutputNode = selectedBaseNode.Node;
      } else {
        desiredDebugOutputTextureKey = null;
        _selectedOutputNode = null;
      }
      PluginService.Instance.DebugOutputTextureKey = desiredDebugOutputTextureKey;
    }

    private static bool ColoredButton(string label, Color color) {
      Color oldBackgroundColor = GUI.backgroundColor;
      GUI.backgroundColor = color;
      bool result = GUILayout.Button(label);
      GUI.backgroundColor = oldBackgroundColor;
      return result;
    }

    public static TestWindow Instance;

    private class Styles {
      public Styles() {
        MiniLabel = EditorStyles.miniLabel;
        MiniLabelRight = new GUIStyle(MiniLabel);
        MiniLabelRight.alignment = TextAnchor.MiddleRight;
      }

      public GUIStyle MiniLabel;
      public GUIStyle MiniLabelRight;
    }

    [MenuItem("Do/Show Monitor Window")]
    public static void ShowWindow() {
      EditorWindow.GetWindow<TestWindow>("Monitor").Show();
    }
  }
}
