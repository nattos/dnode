using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NanoGraph.Plugin {
  public class PluginBuilder {
    public static string ProjectPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "NanoFFGL");

    private bool _isDirty = false;
    private bool _isBuilding = false;

    public bool IsError { get; private set; } = false;
    public bool IsCompiling => _isDirty || _isBuilding;
    public int CompileEpoch { get; private set; } = 0;

    public IReadOnlyList<string> CompileErrors { get { lock(_compileErrorsLock) { return _compileErrors; } } }
    private IReadOnlyList<string> _compileErrors = Array.Empty<string>();
    private object _compileErrorsLock = new object();

    public void MarkDirty() {
      _isDirty = true;
      StartBuilding();
    }

    private void StartBuilding() {
      if (_isBuilding) {
        return;
      }
      ++CompileEpoch;
      _isDirty = false;
      _isBuilding = true;
      IsError = false;
      EditorUtils.DelayCall += async () => {
        bool success = await Task.Run(() => {
          return DoBuild();
        });
        IsError = !success;
        _isBuilding = false;
        if (_isDirty) {
          StartBuilding();
        }
      };
    }
    private static readonly Regex _errorLinePattern =  new Regex(@"^.*:[0-9]+:[0-9]+: error: .*$");
    private static readonly Regex _warningLinePattern =  new Regex(@"^.*:[0-9]+:[0-9]+: warning: .*$");
    private static readonly Regex _endOfErrorsLinePattern =  new Regex(@"[0-9]+ warnings and [0-9]+ errors generated.");

    private bool DoBuild() {
      UnityEngine.Debug.Log("Beginning build.");

      ProcessStartInfo startParams = new ProcessStartInfo();
      startParams.RedirectStandardInput = true;
      startParams.RedirectStandardOutput = true;
      startParams.RedirectStandardError = true;
      startParams.UseShellExecute = false;
      startParams.WorkingDirectory = ProjectPath;
      startParams.FileName = "/usr/bin/xcodebuild";
      startParams.ArgumentList.Add("-target");
      startParams.ArgumentList.Add("NanoFFGL");
      startParams.ArgumentList.Add("-configuration");
      startParams.ArgumentList.Add("Debug");
      startParams.ArgumentList.Add("-scheme");
      startParams.ArgumentList.Add("NanoFFGL");

      Process process = new Process();
      process.StartInfo = startParams;

      process.Start();
      process.WaitForExit();

      string outputStr = process.StandardOutput.ReadToEnd();
      string errorStr = process.StandardError.ReadToEnd();
      int resultCode = process.ExitCode;

      UnityEngine.Debug.Log($"Done build: Code {resultCode}\n{outputStr}\n{errorStr}");

      string[] lines = outputStr.Split("\n");
      List<string> currentSegment = new List<string>();
      List<string> errors =  new List<string>();
      bool isErrorSegment = false;
      void PushErrorSegment() {
        FinishPushSegment();
        isErrorSegment = true;
      }
      void PushWarningSegment() {
        FinishPushSegment();
      }
      void PushNullSegment() {
        FinishPushSegment();
      }
      void FinishPushSegment() {
        if (isErrorSegment) {
          errors.Add(string.Join("\n", currentSegment));
        }
        isErrorSegment = false;
        currentSegment.Clear();
      }

      for (int i = 0; i < lines.Length; ++i) {
        string line = lines[i];
        if (_errorLinePattern.IsMatch(line)) {
          PushErrorSegment();
        } else if (_warningLinePattern.IsMatch(line)) {
          PushWarningSegment();
        } else if (_endOfErrorsLinePattern.IsMatch(line)) {
          PushNullSegment();
        }
        currentSegment.Add(line);
      }
      FinishPushSegment();
      lock (_compileErrorsLock) {
        _compileErrors = errors.ToArray();
      }

      return resultCode == 0;
    }
  }
}
