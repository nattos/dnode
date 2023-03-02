using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System;

namespace NanoGraph {
  public class AssetChangeDetector : ScriptableObject {
    public Action<string> AssetPathChanged;
    private FileSystemWatcher _fileWatcher;

    public AssetChangeDetector() {
      string directoryPath = Path.GetDirectoryName(Application.dataPath);
      FileSystemWatcher watcher = new FileSystemWatcher(directoryPath);
      _fileWatcher = watcher;

      watcher.NotifyFilter = NotifyFilters.Attributes
                            | NotifyFilters.CreationTime
                            | NotifyFilters.DirectoryName
                            | NotifyFilters.FileName
                            | NotifyFilters.LastAccess
                            | NotifyFilters.LastWrite
                            | NotifyFilters.Security
                            | NotifyFilters.Size;
      string assetPathBase = Path.GetDirectoryName(Application.dataPath);
      watcher.Changed += (sender, e) => {
        string path = e.FullPath;
        if (path.StartsWith(assetPathBase)) {
          string assetPath = path.Substring(assetPathBase.Length + 1);
          AssetPathChanged?.Invoke(assetPath);
        }
      };

      watcher.Filter = "*.*";
      watcher.IncludeSubdirectories = true;
      watcher.EnableRaisingEvents = true;
    }

    private static AssetChangeDetector _cachedInstance;

    public static AssetChangeDetector Instance {
      get {
        if (_cachedInstance) {
          return _cachedInstance;
        }
        AssetChangeDetector instance = Resources.FindObjectsOfTypeAll(typeof(AssetChangeDetector)).FirstOrDefault() as AssetChangeDetector;
        if (!instance) {
          instance = ScriptableObject.CreateInstance<AssetChangeDetector>();
        }
        _cachedInstance = instance;
        return instance;
      }
    }
  }
}
