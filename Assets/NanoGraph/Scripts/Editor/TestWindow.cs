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

    public bool Input;
    public bool Locked;

    private double _lastRenderTime;
    private double _lastRenderFrameTime;
    private IDataNode _selectedOutputNode;

    private float _defaultInputTexturePhase = 0.0f;

    private bool _wasCompiling;
    private double _compileStartTime = 0;
    private int _lastCompileEpoch = 0;

    private readonly Lazy<Styles> _styles = new Lazy<Styles>(() => new Styles());

    public void OnEnable() {
      Instance = this;
      SelectedInputs = SelectedInputs ?? new List<SelectedInput>();
      _lastRenderTime = Time.realtimeSinceStartupAsDouble;
      _compileStartTime = Time.realtimeSinceStartupAsDouble;
      PluginService.Instance.TextureInputsNeedUpdating += OnTextureInputsNeedUpdating;
      PluginService.Instance.TextureOutputsUpdated += OnTextureOutputsUpdated;
    }

    public void OnDisable() {
      Instance = null;
      PluginService.Instance.TextureInputsNeedUpdating -= OnTextureInputsNeedUpdating;
      PluginService.Instance.TextureOutputsUpdated -= OnTextureOutputsUpdated;
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
          Graphics.CopyTexture(sourceTexture, inputTexture);
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
      Texture2D monitorTexture;
      string monitorDisplayString;
      if (Input) {
        monitorTexture = PluginService.Instance.GetTextureInput();
        monitorDisplayString = "Input0";
      } else if (!string.IsNullOrEmpty(PluginService.Instance.DebugOutputTextureKey)) {
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
      float aspectRatio = 9.0f / 16.0f;
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
        Locked = EditorGUILayout.ToggleLeft("Locked", Locked);
        EditorGUILayout.Space(0, expand: true);
      }

      Event e = Event.current;
      if (e.button == 0 && e.clickCount == 2 && textureRect.Contains(e.mousePosition)) {
        this.maximized = !this.maximized;
      }

      if (!Locked) {
        // Update the active debug node.
        var graphWindow = global::NanoGraph.VisualScripting.GraphWindow.active;
        var context = graphWindow?.context;
        var selection = context?.selection;
        bool hasSelection = selection?.Count > 0;
        global::NanoGraph.VisualScripting.BaseNode selectedBaseNode = selection.FirstOrDefault() as global::NanoGraph.VisualScripting.BaseNode;
        if (hasSelection) {
          string desiredDebugOutputTextureKey;
          if (selectedBaseNode != null) {
            string nodeDebugId = selectedBaseNode.Node?.DebugId ?? "";
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
      }
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
