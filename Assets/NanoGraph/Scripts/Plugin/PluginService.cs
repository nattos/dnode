using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NanoGraph.Plugin {
  public struct Parameter {
    public string Name;
    public double Value;
    public string StringValue;
    public double DefaultValue;
    public string DefaultStringValue;
    public double MinValue;
    public double MaxValue;
    public NanoValueInputType Type;
  }

  public class PluginService : ScriptableObject {
    private class CancelationFlag {
      public bool Canceled = false;
    }

    private const int TargetFrameRate = 60;
    private const double TargetFrameDelay = 1.0 / TargetFrameRate;

    private PluginBuilder _pluginBuilder;
    private PluginServer _server;
    private PluginWatcher _pluginWatcher;

    [NonSerialized]
    private bool _isRendering = false;
    private CancelationFlag _renderLoopCancelationFlag;

    [NonSerialized]
    public Vector2Int RenderSize = new Vector2Int(1920, 1080);
    public string DebugOutputTextureKey = "";

    private readonly List<SharedTexture> _textureInputs = new List<SharedTexture>();
    private readonly List<SharedTexture> _textureOutputs = new List<SharedTexture>();
    private Dictionary<string, double> _parameterValues = new Dictionary<string, double>();
    private Dictionary<string, string> _parameterStringValues = new Dictionary<string, string>();
    private readonly Dictionary<string, double> _queuedParameterValues = new Dictionary<string, double>();
    private readonly Dictionary<string, string> _queuedParameterStringValues = new Dictionary<string, string>();
    private readonly Dictionary<string, double[]> _queuedDebugSetValues = new Dictionary<string, double[]>();

    private readonly Dictionary<string, double[]> _debugValues = new Dictionary<string, double[]>();

    private readonly List<Parameter> _parameters = new List<Parameter>();

    private bool _isServerStarting = false;

    public Action TextureInputsNeedUpdating;
    public Action TextureOutputsUpdated;

    public bool EnableAutoReload { get; set; } = true;
    public bool EnableDebug { get; set; } = true;
    public bool IsPaused { get; set; } = false;
    public bool RequestStepOnce { get; set; } = false;

    public bool IsCompiling => _pluginWatcher.IsCompiling || _pluginBuilder.IsCompiling;
    public bool HasCompileError => _pluginBuilder.IsError;
    public bool IsReloading => _pluginWatcher.IsReloading || _isServerStarting;
    public int CompileEpoch => _pluginBuilder.CompileEpoch;
    public IReadOnlyList<string> CompileErrors => _pluginBuilder.CompileErrors;

    private Int32 _debugOutputTextureSurfaceId = 0;
    private SharedTexture _debugOutputTexture;

    public PluginService() {
      _pluginBuilder = new PluginBuilder();
      _pluginWatcher = new PluginWatcher();
      _pluginWatcher.PluginBinaryChanged += () => {
        EditorUtils.DelayCall += () => {
          if (EnableAutoReload && IsRendering) {
            Debug.Log("Reloading plugin.");
            StopRendering();
            StartRendering();
          }
        };
      };
      _pluginWatcher.PluginCodeChanged += () => {
        EditorUtils.DelayCall += () => {
          if (EnableAutoReload) {
            _pluginBuilder.MarkDirty();
          }
        };
      };
      if (EnableAutoReload) {
        StartRendering();
      }
    }

    public void ExportPlugin(string exportAs) {
      _pluginBuilder.ExportPlugin(exportAs);
    }

    public Texture2D GetTextureInput() {
      return _textureInputs.FirstOrDefault()?.Texture;
    }

    public Texture2D[] GetTextureInputs() {
      return _textureInputs.Select(t => t.Texture).ToArray();
    }

    public int TextureInputCount => _textureInputs.Count;

    public Texture2D GetTextureOutput() {
      return _textureOutputs.FirstOrDefault()?.Texture;
    }

    public Texture2D GetDebugOutputTexture() {
      return _debugOutputTexture?.Texture;
    }

    private struct RenderResult {
      public Action PushResultsOnMainThread;
    }
    private Task<RenderResult> _inFlightRenderRequest = null;

    public void PushRenderRequest() {
      if (_inFlightRenderRequest != null) {
        if (_inFlightRenderRequest?.IsCompleted == true) {
          var task = _inFlightRenderRequest;
          _inFlightRenderRequest = null;
          try {
            ConsumeRenderResult(task.Result);
          } catch (Exception e) {
            Debug.Log(e);
          }
        } else {
          return;
        }
      }
      BeginRenderRequest();
    }

    float phase = 0.0f;
    int _frameCount = 0;
    private void BeginRenderRequest() {
      if (_server == null) {
        return;
      }
      if (_textureInputs.Count > 0) {
        TextureInputsNeedUpdating?.Invoke();
      }

      Dictionary<string, double> queuedParameterValues = null;
      Dictionary<string, string> queuedParameterStringValues = null;
      if (_queuedParameterValues.Count > 0) {
        queuedParameterValues = _queuedParameterValues.ToDictionary();
        _queuedParameterValues.Clear();
      }
      if (_queuedParameterStringValues.Count > 0) {
        queuedParameterStringValues = _queuedParameterStringValues.ToDictionary();
        _queuedParameterStringValues.Clear();
      }
      Dictionary<string, double[]> queuedDebugSetValues = null;
      if (_queuedDebugSetValues.Count > 0) {
        queuedDebugSetValues = _queuedDebugSetValues.ToDictionary();
        _queuedDebugSetValues.Clear();
      }

      bool enableDebug = EnableDebug;
      int frameCount = _frameCount++;
      bool isForceStep = RequestStepOnce;
      bool shouldStep = !IsPaused || RequestStepOnce;
      RequestStepOnce = false;

      _inFlightRenderRequest = Task.Run(async () => {
        if (queuedParameterValues != null || queuedParameterStringValues != null) {
          await _server.SetParametersRequest(queuedParameterValues, queuedParameterStringValues);
        }
        if (queuedDebugSetValues != null) {
          await _server.DebugSetValues(queuedDebugSetValues.Select(entry => new DebugSetValuesRequest.Value { Key = entry.Key, Values = entry.Value }).ToArray());
        }

        ProcessTexturesResponse response = shouldStep ? await _server.ProcessTextures(_textureInputs, _textureOutputs, DebugOutputTextureKey) : null;
        bool updateDebugValues = ((frameCount % 2) == 0 || isForceStep) && enableDebug;
        DebugGetWatchedValuesResponse debugValues = updateDebugValues ? await _server.DebugGetWatchedValues() : null;

        void PushResultsOnMainThread() {
          if (response != null && response.DebugOutputTexture != 0 && response.DebugOutputTexture != _debugOutputTextureSurfaceId) {
            _debugOutputTextureSurfaceId = 0;
            _debugOutputTexture?.Dispose();
            _debugOutputTexture = null;
            _debugOutputTexture = SharedTextureManager.Instance.CreateTextureFromSurfaceId(response.DebugOutputTexture);
            if (_debugOutputTexture != null) {
              _debugOutputTextureSurfaceId = response.DebugOutputTexture;
            }
          }

          if (updateDebugValues) {
            _debugValues.Clear();
            if (debugValues?.Values?.Length > 0) {
              foreach (var debugValue in debugValues.Values) {
                _debugValues[debugValue.Key] = debugValue.Values;
              }
            }
          } else if (!enableDebug) {
            _debugValues.Clear();
          }

          if (shouldStep) {
            TextureOutputsUpdated?.Invoke();
          }
        }
        return new RenderResult { PushResultsOnMainThread = PushResultsOnMainThread };
      });
    }

    private void ConsumeRenderResult(RenderResult request) {
      request.PushResultsOnMainThread?.Invoke();
    }


    public bool IsRendering => _isRendering;

    public void StartRendering() {
      if (_isRendering) {
        return;
      }
      _isServerStarting = true;
      _isRendering = true;
      var thisCancelationFlag = new CancelationFlag();
      _renderLoopCancelationFlag = thisCancelationFlag;
      EditorUtils.DelayCall += async () => {
        bool didStart = false;
        try {
          double frameStartTime = Time.realtimeSinceStartupAsDouble;
          while (true) {
            if (thisCancelationFlag.Canceled) {
              _isServerStarting = false;
              return;
            }

            try {
              await StartServer();
              _isServerStarting = false;
              didStart = true;
            } catch (Exception e) {
              Debug.Log(e);
            }

            PushRenderRequest();

            double frameEndTime = Time.realtimeSinceStartupAsDouble;
            double waitSeconds = TargetFrameDelay - (frameEndTime - frameStartTime);
            int waitMillis = (int)Math.Round(1000 * waitSeconds);
            await Task.Delay(Mathf.Max(0, waitMillis));
            frameStartTime = frameEndTime;
          }
        } catch (Exception e) {
          if (!didStart) {
            _isServerStarting = false;
          }
          Debug.Log(e);
        }
      };
    }

    public void StopRendering() {
      if (!_isRendering) {
        return;
      }
      _isRendering = false;
      if (_renderLoopCancelationFlag != null) {
        _renderLoopCancelationFlag.Canceled = true;
        _renderLoopCancelationFlag = null;
      }
      StopServer();
    }

    public Parameter[] GetParameters() {
      Parameter[] parameters = _parameters.ToArray();
      for (int i = 0; i < parameters.Length; ++i) {
        parameters[i].Value = _parameterValues.GetOrDefault(parameters[i].Name);
        parameters[i].StringValue = _parameterStringValues.GetOrDefault(parameters[i].Name);
      }
      return parameters;
    }

    public void SetParameter(string name, double value) {
      _parameterValues[name] = value;
      _queuedParameterValues[name] = value;
    }

    public void SetParameterString(string name, string value) {
      _parameterStringValues[name] = value;
      _queuedParameterStringValues[name] = value;
    }

    public double[] GetDebugValues(string key) {
      return _debugValues.GetOrDefault(key);
    }

    public void SetDebugValue(string key, double[] values) {
      _queuedDebugSetValues[key] = values;
    }

    private async Task StartServer() {
      if (_server?.IsAlive == true) {
        return;
      }
      StopServer();
      _server = new PluginServer();

      var definition = await _server.GetDefinition();
      int textureInputCount = definition.TextureInputCount;

      ClearTextures();
      for (int i = 0; i < textureInputCount; ++i) {
        _textureInputs.Add(SharedTextureManager.Instance.CreateTexture(RenderSize.x, RenderSize.y));
      }
      _textureOutputs.Add(SharedTextureManager.Instance.CreateTexture(RenderSize.x, RenderSize.y));

      _parameters.Clear();
      Dictionary<string, double> oldParameterValues = _parameterValues;
      Dictionary<string, double> newParameterValues = new Dictionary<string, double>();
      Dictionary<string, double> parameterValuesToSet = new Dictionary<string, double>();
      var parameters = await _server.GetParameters();
      foreach (var parameter in parameters.Parameters ?? Array.Empty<GetParametersResponse.Parameter>()) {
        if (oldParameterValues.TryGetValue(parameter.Name, out double value)) {
          parameterValuesToSet[parameter.Name] = value;
        } else {
          value = parameter.Value;
        }
        newParameterValues[parameter.Name] = value;
        _parameters.Add(new Parameter {
          Name = parameter.Name,
          DefaultValue = parameter.DefaultValue,
          DefaultStringValue = parameter.DefaultStringValue,
          MinValue = parameter.MinValue,
          MaxValue = parameter.MaxValue,
          Type = Enum.TryParse<NanoValueInputType>(parameter.Type, out NanoValueInputType type) ? type : NanoValueInputType.Float,
        });
      }
      if (parameterValuesToSet.Count > 0) {
        await _server.SetParametersRequest(parameterValuesToSet);
      }
      _parameterValues = newParameterValues;
    }

    private void StopServer() {
      _server?.Dispose();
      _server = null;
    }

    private void ClearTextures() {
      foreach (SharedTexture texture in _textureInputs.Concat(_textureOutputs)) {
        texture.Dispose();
      }
      _textureInputs.Clear();
      _textureOutputs.Clear();
    }

    public void OnDisable() {
      StopServer();
      StopRendering();
      ClearTextures();
    }


    private static PluginService _cachedInstance;

    public static PluginService Instance {
      get {
        if (_cachedInstance) {
          return _cachedInstance;
        }
        PluginService instance = Resources.FindObjectsOfTypeAll(typeof(PluginService)).FirstOrDefault() as PluginService;
        if (!instance) {
          instance = ScriptableObject.CreateInstance<PluginService>();
        }
        _cachedInstance = instance;
        return instance;
      }
    }
  }
}
