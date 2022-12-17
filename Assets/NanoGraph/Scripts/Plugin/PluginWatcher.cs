using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace NanoGraph.Plugin {
  public class PluginWatcher {
    private const int RecompileDelayMillis = 50;

    private FileSystemWatcher _codeWatcher;
    private FileSystemWatcher _pluginWatcher;

    private bool _isRecompiling = false;
    private bool _isReloading = false;

    public Action PluginCodeChanged;
    public Action PluginBinaryChanged;

    public PluginWatcher() {
      {
        FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(NanoGraph.GeneratedCodeOutputPath));
        _codeWatcher = watcher;

        watcher.NotifyFilter = NotifyFilters.Attributes
                              | NotifyFilters.CreationTime
                              | NotifyFilters.DirectoryName
                              | NotifyFilters.FileName
                              | NotifyFilters.LastAccess
                              | NotifyFilters.LastWrite
                              | NotifyFilters.Security
                              | NotifyFilters.Size;
        watcher.Changed += (sender, e) => MaybeRecompileLater();
        watcher.Created += (sender, e) => MaybeRecompileLater();
        watcher.Deleted += (sender, e) => MaybeRecompileLater();
        watcher.Renamed += (sender, e) => MaybeRecompileLater();
        watcher.Error += (sender, e) => MaybeRecompileLater();

        watcher.Filter = "*.*";
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
      }
      {
        FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(PluginServer.PluginPackagePath));
        _pluginWatcher = watcher;

        watcher.NotifyFilter = NotifyFilters.Attributes
                              | NotifyFilters.CreationTime
                              | NotifyFilters.DirectoryName
                              | NotifyFilters.FileName
                              | NotifyFilters.LastAccess
                              | NotifyFilters.LastWrite
                              | NotifyFilters.Security
                              | NotifyFilters.Size;
        watcher.Changed += (sender, e) => MaybeReloadLater();
        watcher.Created += (sender, e) => MaybeReloadLater();
        watcher.Deleted += (sender, e) => MaybeReloadLater();
        watcher.Renamed += (sender, e) => MaybeReloadLater();
        watcher.Error += (sender, e) => MaybeReloadLater();

        watcher.Filter = "*.*";
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
      }
    }

    public bool IsCompiling => _isRecompiling;

    private void MaybeRecompileLater() {
      // Delegate to main thread.
      EditorUtils.DelayCall += () => {
        if (_isRecompiling) {
          return;
        }
        _isRecompiling = true;
        EditorUtils.DelayCall += async () => {
          await Task.Delay(RecompileDelayMillis);
          _isRecompiling = false;
          PluginCodeChanged?.Invoke();
        };
      };
    }

    public bool IsReloading => _isReloading;

    private void MaybeReloadLater() {
      // Delegate to main thread.
      EditorUtils.DelayCall += () => {
        if (_isReloading) {
          return;
        }
        _isReloading = true;
        EditorUtils.DelayCall += () => {
          _isReloading = false;
          if (File.Exists(PluginServer.PluginBinaryPath)) {
            PluginBinaryChanged?.Invoke();
          }
        };
      };
    }
  }
}
