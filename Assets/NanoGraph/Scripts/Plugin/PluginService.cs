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
    public double DefaultValue;
    public double MinValue;
    public double MaxValue;
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

    public Vector2Int RenderSize = new Vector2Int(1920, 1080);
    public string DebugOutputTextureKey = "";

    private readonly List<SharedTexture> _textureInputs = new List<SharedTexture>();
    private readonly List<SharedTexture> _textureOutputs = new List<SharedTexture>();
    private Dictionary<string, double> _parameterValues = new Dictionary<string, double>();
    private readonly Dictionary<string, double> _queuedParameterValues = new Dictionary<string, double>();
    private readonly Dictionary<string, double[]> _queuedDebugSetValues = new Dictionary<string, double[]>();

    private readonly Dictionary<string, double[]> _debugValues = new Dictionary<string, double[]>();

    private readonly List<Parameter> _parameters = new List<Parameter>();

    private bool _isServerStarting = false;

    public Action TextureOutputsUpdated;

    public bool EnableAutoReload { get; set; } = true;

    public bool IsCompiling => _pluginWatcher.IsCompiling || _pluginBuilder.IsCompiling;
    public bool HasCompileError => _pluginBuilder.IsError;
    public bool IsReloading => _pluginWatcher.IsReloading || _isServerStarting;

    private Int32 _debugOutputTextureSurfaceId = 0;
    private SharedTexture _debugOutputTexture;

    public PluginService() {
      _pluginBuilder = new PluginBuilder();
      _pluginWatcher = new PluginWatcher();
      _pluginWatcher.PluginBinaryChanged += () => {
        if (EnableAutoReload && IsRendering) {
          Debug.Log("Reloading plugin.");
          StopRendering();
          StartRendering();
        }
      };
      _pluginWatcher.PluginCodeChanged += () => {
        if (EnableAutoReload) {
          _pluginBuilder.MarkDirty();
        }
      };
      if (EnableAutoReload) {
        StartRendering();
      }
    }

    public Texture2D GetTextureInput() {
      return _textureInputs.FirstOrDefault()?.Texture;
    }

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
    private void BeginRenderRequest() {
      if (_server == null) {
        return;
      }
      if (_textureInputs.Count > 0) {
        var _serverTexture = _textureInputs[_textureInputs.Count - 1].Texture;
        var temp = RenderTexture.GetTemporary(
            _serverTexture.width, _serverTexture.height, 0,
            RenderTextureFormat.Default, RenderTextureReadWrite.Default
        );
        RenderTexture oldRT = RenderTexture.active;
        RenderTexture.active = temp;
        phase += 0.05f;
        GL.Clear(clearDepth: false, clearColor: true, new Color(Mathf.Abs(Mathf.Sin(phase)), 0.0f, 0.0f, 1.0f));
        // GL.PushMatrix();
        // GL.LoadOrtho();
        // GL.LoadPixelMatrix();
        // GL.Begin(GL.LINES);
        // GL.Vertex3(0.0f, 0.0f, 0.0f);
        // GL.Vertex3(10.0f, 10.0f, 10.0f);
        // GL.Color(Color.red);
        // GL.End();
        // GL.PopMatrix();
        RenderTexture.active = oldRT;
        Graphics.CopyTexture(temp, _serverTexture);
        // RenderTexture.ReleaseTemporary(temp);
        // SharedTexture tex = _textureInputs[_textureInputs.Count - 1];
        // Graphics.CopyTexture(Texture2D.redTexture, 0, 0, 0, 0, Texture2D.redTexture.width, Texture2D.redTexture.height, tex.Texture, 0, 0, 100, 100);
        // SharedTexture tex = _textureInputs[_textureInputs.Count - 1];
        // RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.BGRA32, 0, );
        // RenderTexture.active = rt;
        // phase += 0.1f;
        // GL.Clear(clearDepth: false, clearColor: true, new Color(Mathf.Abs(Mathf.Sin(phase)), 0.0f, 0.0f, 1.0f));
        // RenderTexture.active = null;
        // // // var rt = RenderTexture.GetTemporary(tex.Texture.width, tex.Texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
        // Graphics.CopyTexture(rt, tex.Texture);
        // RenderTexture.ReleaseTemporary(rt);
      }

      Dictionary<string, double> queuedParameterValues = null;
      if (_queuedParameterValues.Count > 0) {
        queuedParameterValues = _queuedParameterValues.ToDictionary();
        _queuedParameterValues.Clear();
      }
      Dictionary<string, double[]> queuedDebugSetValues = null;
      if (_queuedDebugSetValues.Count > 0) {
        queuedDebugSetValues = _queuedDebugSetValues.ToDictionary();
        _queuedDebugSetValues.Clear();
      }

      _inFlightRenderRequest = Task.Run(async () => {
        if (queuedParameterValues != null) {
          await _server.SetParametersRequest(queuedParameterValues);
        }
        if (queuedDebugSetValues != null) {
          await _server.DebugSetValues(queuedDebugSetValues.Select(entry => new DebugSetValuesRequest.Value { Key = entry.Key, Values = entry.Value }).ToArray());
        }

        var response = await _server.ProcessTextures(_textureInputs, _textureOutputs, DebugOutputTextureKey);
        DebugGetWatchedValuesResponse debugValues = await _server.DebugGetWatchedValues();

        void PushResultsOnMainThread() {
          if (response.DebugOutputTexture != 0 && response.DebugOutputTexture != _debugOutputTextureSurfaceId) {
            _debugOutputTextureSurfaceId = response.DebugOutputTexture;
            _debugOutputTexture?.Dispose();
            _debugOutputTexture = null;
            _debugOutputTexture = SharedTextureManager.Instance.CreateTextureFromSurfaceId(response.DebugOutputTexture);
          }

          _debugValues.Clear();
          if (debugValues?.Values?.Length > 0) {
            foreach (var debugValue in debugValues.Values) {
              _debugValues[debugValue.Key] = debugValue.Values;
            }
          }

          TextureOutputsUpdated?.Invoke();
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
        double frameStartTime = Time.realtimeSinceStartupAsDouble;
        while (true) {
          if (thisCancelationFlag.Canceled) {
            _isServerStarting = false;
            return;
          }

          try {
            await StartServer();
            _isServerStarting = false;
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
      }
      return parameters;
    }

    public void SetParameter(string name, double value) {
      _parameterValues[name] = value;
      _queuedParameterValues[name] = value;
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

        // SharedTexture tex = _textureInputs[_textureInputs.Count - 1];
        // Graphics.CopyTexture(Texture2D.redTexture, 0, 0, 0, 0, Texture2D.redTexture.width, Texture2D.redTexture.height, tex.Texture, 0, 0, 100, 100);
        // RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.BGRA32, 0);
        // RenderTexture.active = rt;
        // GL.Clear(clearDepth: false, clearColor: true, Color.yellow);
        // RenderTexture.active = null;
        // Graphics.CopyTexture(rt, 0, 0, 0, 0, rt.width, rt.height, tex.Texture, 0, 0, 0, 0);
        // RenderTexture.ReleaseTemporary(rt);
        // SharedTexture tex = _textureInputs[_textureInputs.Count - 1];
        // RenderTexture rt = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.BGRA32, 0);
        // RenderTexture.active = rt;
        // GL.Clear(clearDepth: false, clearColor: true, Color.white);
        // RenderTexture.active = null;
        // // var rt = RenderTexture.GetTemporary(tex.Texture.width, tex.Texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
        // Graphics.CopyTexture(rt, tex.Texture);
        // RenderTexture.ReleaseTemporary(rt);

        // var fence = Graphics.CreateGraphicsFence(UnityEngine.Rendering.GraphicsFenceType.AsyncQueueSynchronisation, UnityEngine.Rendering.SynchronisationStageFlags.AllGPUOperations);
        // Graphics.WaitOnAsyncGraphicsFence(fence);
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
          MinValue = parameter.MinValue,
          MaxValue = parameter.MaxValue,
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


    [UnityEditor.MenuItem("Do/Start Rendering")]
    public static void DoStartRendering() {
      PluginService.Instance.StartRendering();
    }

    [UnityEditor.MenuItem("Do/Stop Rendering")]
    public static void DoStopRendering() {
      PluginService.Instance.StopRendering();
    }

      // // SharedTexture texture = Instance.CreateTexture(1920, 1080);

      // using (var server = new PluginServer()) {
      //   // server.SendRequest(new Request {
      //   //   GetDefinition = new GetDefinitionRequest {}
      //   // }, MakeRequestHandler<GetDefinitionResponse>(response => UnityEngine.Debug.Log(response)));
      //   // server.SendRequest(new Request {
      //   //   GetDefinition = new GetDefinitionRequest {}
      //   // });
      //   // System.Threading.Thread.Sleep(1000);

      //   UnityEngine.Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(await server.GetDefinition()));
      //   UnityEngine.Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(await server.GetParameters()));
      //   UnityEngine.Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(await server.SetParametersRequest(new[] {
      //     new KeyValuePair<string, double>("My Param", 5.0),
      //   })));
      //   UnityEngine.Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(await server.GetParameters()));

      // }
    // }
  }
}
