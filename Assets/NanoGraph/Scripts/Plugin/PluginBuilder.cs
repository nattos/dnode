using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace NanoGraph.Plugin {
  public class PluginBuilder {
    public static string ProjectPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), "NanoFFGL");

    private bool _isDirty = false;
    private bool _isBuilding = false;

    public void MarkDirty() {
      _isDirty = true;
      StartBuilding();
    }

    private void StartBuilding() {
      if (_isBuilding) {
        return;
      }
      _isDirty = false;
      _isBuilding = true;
      EditorUtils.DelayCall += async () => {
        await Task.Run(() => {
          DoBuild();
        });
        _isBuilding = false;
        if (_isDirty) {
          StartBuilding();
        }
      };
    }

    private void DoBuild() {
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
    }
  }
}
