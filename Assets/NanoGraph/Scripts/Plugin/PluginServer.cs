using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoGraph.Plugin {
  public class Request {
    public GetDefinitionRequest GetDefinition;
    public GetParametersRequest GetParameters;
    public SetParametersRequest SetParameters;
    public ProcessTexturesRequest ProcessTextures;
  }

  public class GetDefinitionRequest {}
  public class GetDefinitionResponse {
    public string Name;
    public int TextureInputCount;
  }

  public class GetParametersRequest {}
  public class GetParametersResponse {
    public class Parameter {
      public string Name;
      public double Value;
      public double DefaultValue;
      public double MaxValue;
      public double MinValue;
    }
    public Parameter[] Parameters;
  }

  public class SetParametersRequest {
    public Dictionary<string, double> Values;
  }
  public class SetParametersResponse {}

  public class ProcessTexturesRequest {
    public Int32[] TextureInputs;
    public Int32[] TextureOutputs;
  }
  public class ProcessTexturesResponse {}

  public class PluginServer : IDisposable {
    public static string PluginPackagePath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "NanoFFGL/build/NanoFFGL/Build/Products/Debug/NanoFFGL.app");
    public static string PluginBinaryPath => System.IO.Path.Combine(PluginPackagePath, "Contents/MacOS/NanoFFGL");

    private const bool DebugStandardError = false;

    private readonly AutoResetEvent _threadFlag = new AutoResetEvent(false);
    private readonly ManualResetEvent _terminateFlag = new ManualResetEvent(false);

    private readonly object _commandQueueLock = new object();
    private readonly List<(string, Action<string>)> _commandQueue = new List<(string, Action<string>)>();

    private readonly object _outputLock = new object();
    private readonly StringBuilder _outputBuffer = new StringBuilder();

    private readonly object _terminatedLock = new object();
    private bool _terminated = false;

    private readonly Process _process;
    private readonly Thread _thread;

    public PluginServer() {
      _process = new Process();
      _thread = new Thread(ThreadProc);
      _thread.Start();
    }

    public bool IsAlive => _thread.IsAlive;

    public async Task<GetDefinitionResponse> GetDefinition() {
      return await SendRequestAsync<GetDefinitionResponse>(new Request {
        GetDefinition = new GetDefinitionRequest {}
      });
    }

    public async Task<GetParametersResponse> GetParameters() {
      return await SendRequestAsync<GetParametersResponse>(new Request {
        GetParameters = new GetParametersRequest {}
      });
    }

    public async Task<SetParametersResponse> SetParametersRequest(IEnumerable<KeyValuePair<string, double>> values) {
      return await SendRequestAsync<SetParametersResponse>(new Request {
        SetParameters = new SetParametersRequest {
          Values = values.ToDictionary(entry => entry.Key, entry => entry.Value),
        }
      });
    }

    public async Task<ProcessTexturesResponse> ProcessTextures(IReadOnlyList<SharedTexture> textureInputs, IReadOnlyList<SharedTexture> textureOutputs) {
      return await SendRequestAsync<ProcessTexturesResponse>(new Request {
        ProcessTextures = new ProcessTexturesRequest {
          TextureInputs = textureInputs.Select(t => t.IOSurfaceID).ToArray(),
          TextureOutputs = textureOutputs.Select(t => t.IOSurfaceID).ToArray(),
        }
      });
    }

    private async Task<T> SendRequestAsync<T>(Request request) where T : class {
      ManualResetEvent flag = new ManualResetEvent(false);
      T response = null;

      return await Task.Run(() => {
        SendRequest(request, MakeRequestHandler<T>(r => { response = r; flag.Set(); }));
        flag.WaitOne();
        return response;
      });
    }

    private static readonly Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings {
      NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
    };

    private void SendRequest(Request request, Action<string> responseHandler) {
      string command = Newtonsoft.Json.JsonConvert.SerializeObject(request, JsonSerializerSettings);
      // TODO: base64 encode.
      lock (_commandQueueLock)
      lock (_terminatedLock) {
        if (_terminated) {
          FlushCommandHandler(responseHandler);
          return;
        }
        _commandQueue.Add((command, responseHandler));
        _threadFlag.Set();
      }
    }

    private static Action<string> MakeRequestHandler<T>(Action<T> handler) {
      return data => {
        T result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(data);
        handler.Invoke(result);
      };
    }

    private void ThreadProc() {
      Process process = _process;
      ProcessStartInfo startParams = new ProcessStartInfo();
      startParams.RedirectStandardInput = true;
      startParams.RedirectStandardOutput = true;
      if (DebugStandardError) {
        startParams.RedirectStandardError = true;
      }
      startParams.UseShellExecute = false;
      startParams.FileName = PluginBinaryPath;
      process.StartInfo = startParams;

      process.OutputDataReceived += (object sender, DataReceivedEventArgs e) => {
        string data = e.Data;
        if (string.IsNullOrEmpty(data)) {
          return;
        }
        lock (_outputLock) {
          _outputBuffer.AppendLine(data);
        }
        _threadFlag.Set();
      };
      if (DebugStandardError) {
        process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
          string data = e.Data;
          if (data == null) {
            return;
          }
          UnityEngine.Debug.Log(data);
        };
      }

      process.Start();

      Queue<Action<string>> commandHandlerQueue = new Queue<Action<string>>();
      try {
        process.BeginOutputReadLine();
        if (DebugStandardError) {
          process.BeginErrorReadLine();
        }

        List<string> commandQueue = new List<string>();
        StringBuilder outputBuffer = new StringBuilder();
        int outputBufferPos = 0;
        while (true) {
          lock (_commandQueueLock) {
            if (_commandQueue.Count > 0) {
              foreach (var (command, responseHandler) in _commandQueue) {
                commandQueue.Add(command);
                commandHandlerQueue.Enqueue(responseHandler);
              }
              _commandQueue.Clear();
            }
          }
          if (commandQueue.Count > 0) {
            foreach (string command in commandQueue) {
              // UnityEngine.Debug.Log($"Command: {command}");
              process.StandardInput.WriteLine(command);
            }
            commandQueue.Clear();
          }

          bool outputDirty = false;
          lock (_outputLock) {
            if (_outputBuffer.Length > 0) {
              outputBuffer.Append(_outputBuffer);
              _outputBuffer.Clear();
              outputDirty = true;
            }
          }
          if (outputDirty) {
            while (true) {
              string outputLine = null;
              for (; outputBufferPos < outputBuffer.Length; ++outputBufferPos) {
                char c = outputBuffer[outputBufferPos];
                if (c == '\n') {
                  outputLine = outputBuffer.ToString(0, outputBufferPos);
                  outputBuffer.Remove(0, outputBufferPos + 1);
                  outputBufferPos = 0;
                  break;
                }
              }
              if (outputLine == null) {
                break;
              }
              try {
                commandHandlerQueue.Dequeue()?.Invoke(outputLine);
              } catch (Exception e) {
                UnityEngine.Debug.LogException(e);
              }
            }
          }

          _threadFlag.WaitOne();
          if (_terminateFlag.WaitOne(0)) {
            return;
          }
        }
      } finally {
        lock (_commandQueueLock)
        lock (_terminatedLock) {
          _terminated = true;
          foreach (var (_, handler) in _commandQueue) {
            commandHandlerQueue.Enqueue(handler);
          }
          _commandQueue.Clear();
        }
        while (commandHandlerQueue.Count > 0) {
          FlushCommandHandler(commandHandlerQueue.Dequeue());
        }

        if (!process.HasExited) {
          try {
            process.Kill();
          } catch (Exception e) {
            UnityEngine.Debug.LogException(e);
          }
        }
      }
    }

    private void FlushCommandHandler(Action<string> commandResponseHandler) {
      try {
        commandResponseHandler?.Invoke(null);
      } catch (Exception e) {
        UnityEngine.Debug.Log($"Server was shutdown: {e}");
      }
    }

    public void Dispose() {
      _terminateFlag.Set();
      _threadFlag.Set();
      _thread.Join();
    }
  }
}
