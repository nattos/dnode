using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class PrefabCache : IDisposable, IFrameComponent {
    private struct PrefabInstanceKey {
      public readonly object Key;
      public readonly string Tag;
      public readonly int PrefabInstanceId;

      public PrefabInstanceKey(object key, string tag, int prefabInstanceId) {
        Key = key;
        Tag = tag;
        PrefabInstanceId = prefabInstanceId;
      }
    }

    private class PrefabInstanceList {
      public readonly List<GameObject> Instances = new List<GameObject>();
      public int LastUsedFrameNumber = 0;
      public int LastUsedInstanceIndex = 0;

      public void Dispose() {
        foreach (GameObject instance in Instances) {
          DisposeInstance(instance);
        }
        Instances.Clear();
      }

      public static void DisposeInstance(GameObject instance) {
        if (instance) {
          instance.SetActive(false);
          UnityUtils.Destroy(instance);
        }
      }
    }

    private class SceneData {
      public int LastUsedFrameNumber = 0;
      public readonly Dictionary<PrefabInstanceKey, PrefabInstanceList> PrefabInstances = new Dictionary<PrefabInstanceKey, PrefabInstanceList>();
      public readonly List<GameObject> SceneGameObjects = new List<GameObject>();

      public void Dispose() {
        foreach (var entry in PrefabInstances) {
          entry.Value.Dispose();
        }
        PrefabInstances.Clear();
        SceneGameObjects.Clear();
      }
    }

    private static readonly List<IFrameComponent> _staticFrameComponentList = new List<IFrameComponent>();

    private readonly List<SceneData> _scenes = new List<SceneData>();
    private int _currentSceneIndex = 0;
    private int _currentFrameNumber = 0;

    public void Dispose() {
      foreach (var scene in _scenes) {
        scene.Dispose();
      }
      _scenes.Clear();
      _currentFrameNumber = 0;
      _currentSceneIndex = 0;
    }

    public void OnStartFrame() {
      _currentSceneIndex = 0;
      _currentFrameNumber++;

      for (int i = 0; i < _scenes.Count; ++i) {
        SceneData sceneData = _scenes[i];
        foreach (var entry in sceneData.PrefabInstances) {
          foreach (var instance in entry.Value.Instances) {
            _staticFrameComponentList.Clear();
            instance.GetComponentsInChildren<IFrameComponent>(_staticFrameComponentList);
            foreach (IFrameComponent frameComponent in _staticFrameComponentList) {
              frameComponent.OnStartFrame();
            }
          }
        }
        _staticFrameComponentList.Clear();
      }
    }

    public void StartScene() {
      if (_currentSceneIndex >= _scenes.Count) {
        return;
      }
      SceneData sceneData = _scenes[_currentSceneIndex];
      foreach (var entry in sceneData.PrefabInstances) {
        PrefabInstanceList instanceList = entry.Value;
        int unusedStartIndex;
        if (instanceList.LastUsedFrameNumber != _currentFrameNumber) {
          unusedStartIndex = 0;
        } else {
          unusedStartIndex = instanceList.LastUsedInstanceIndex;
        }
        for (int j = unusedStartIndex; j < instanceList.Instances.Count; ++j) {
          PrefabInstanceList.DisposeInstance(instanceList.Instances[j]);
        }
        instanceList.Instances.RemoveRange(unusedStartIndex, instanceList.Instances.Count - unusedStartIndex);
      }
    }

    public void AddSceneGameObjectToScene(GameObject go) {
      SceneData sceneData = GetOrCreateCurrentScene();
      sceneData.SceneGameObjects.Add(go);
      _staticFrameComponentList.Clear();
      go.GetComponentsInChildren<IFrameComponent>(_staticFrameComponentList);
      foreach (IFrameComponent frameComponent in _staticFrameComponentList) {
        frameComponent.OnStartFrame();
      }
      _staticFrameComponentList.Clear();
    }

    public void EndScene() {
      if (_currentSceneIndex >= _scenes.Count) {
        return;
      }
      _currentSceneIndex++;
    }

    public void OnEndFrame() {
      for (int i = 0; i < _scenes.Count; ++i) {
        SceneData sceneData = _scenes[i];
        if (sceneData.LastUsedFrameNumber != _currentFrameNumber) {
          for (int j = i; j < _scenes.Count; ++j) {
            SceneData deletedSceneData = _scenes[j];
            deletedSceneData.Dispose();
          }
          _scenes.RemoveRange(i, _scenes.Count - i);
          break;
        }

        int disposedPrefabInstances = 0;
        foreach (var entry in sceneData.PrefabInstances) {
          PrefabInstanceList instanceList = entry.Value;
          if (instanceList.LastUsedFrameNumber != _currentFrameNumber) {
            disposedPrefabInstances++;
            continue;
          }
          for (int j = instanceList.LastUsedInstanceIndex; j < instanceList.Instances.Count; ++j) {
            PrefabInstanceList.DisposeInstance(instanceList.Instances[j]);
          }
          instanceList.Instances.RemoveRange(instanceList.LastUsedInstanceIndex, instanceList.Instances.Count - instanceList.LastUsedInstanceIndex);
        }

        if (disposedPrefabInstances > 0) {
          List<PrefabInstanceKey> removedKeys = new List<PrefabInstanceKey>(disposedPrefabInstances);
          foreach (var entry in sceneData.PrefabInstances) {
            PrefabInstanceList instanceList = entry.Value;
            if (instanceList.LastUsedFrameNumber != _currentFrameNumber) {
              instanceList.Dispose();
              removedKeys.Add(entry.Key);
            }
          }
          foreach (var key in removedKeys) {
            sceneData.PrefabInstances.Remove(key);
          }
        }

        foreach (var entry in sceneData.PrefabInstances) {
          foreach (var instance in entry.Value.Instances) {
            _staticFrameComponentList.Clear();
            instance.GetComponentsInChildren<IFrameComponent>(_staticFrameComponentList);
            foreach (IFrameComponent frameComponent in _staticFrameComponentList) {
              frameComponent.OnEndFrame();
            }
          }
        }
        foreach (var instance in sceneData.SceneGameObjects) {
          _staticFrameComponentList.Clear();
          instance.GetComponentsInChildren<IFrameComponent>(_staticFrameComponentList);
          foreach (IFrameComponent frameComponent in _staticFrameComponentList) {
            frameComponent.OnEndFrame();
          }
        }
        sceneData.SceneGameObjects.Clear();
        _staticFrameComponentList.Clear();
      }
    }

    public (GameObject instance, bool isNew) InstantiatePrefab(object key, string tag, GameObject prefab) {
      PrefabInstanceKey instanceKey = new PrefabInstanceKey(key, tag, prefab.OrNull()?.GetInstanceID() ?? int.MinValue);

      SceneData sceneData = GetOrCreateCurrentScene();
      if (!sceneData.PrefabInstances.TryGetValue(instanceKey, out PrefabInstanceList instanceList)) {
        instanceList = new PrefabInstanceList();
        sceneData.PrefabInstances[instanceKey] = instanceList;
      }
      if (instanceList.LastUsedFrameNumber != _currentFrameNumber) {
        instanceList.LastUsedFrameNumber = _currentFrameNumber;
        instanceList.LastUsedInstanceIndex = 0;
      }

      bool isNew;
      GameObject instance;
      if (instanceList.LastUsedInstanceIndex >= instanceList.Instances.Count) {
        instance = prefab != null ? GameObject.Instantiate(prefab) : new GameObject();
        instanceList.Instances.Add(instance);
        instanceList.LastUsedInstanceIndex = instanceList.Instances.Count;
        isNew = true;
      } else {
        instance = instanceList.Instances[instanceList.LastUsedInstanceIndex];
        instanceList.LastUsedInstanceIndex++;
        isNew = false;
      }
      return (instance, isNew);
    }

    private SceneData GetOrCreateCurrentScene() {
      if (_currentSceneIndex >= _scenes.Count) {
        _scenes.Add(new SceneData());
      }
      SceneData sceneData = _scenes[_currentSceneIndex];
      sceneData.LastUsedFrameNumber = _currentFrameNumber;
      return sceneData;
    }
  }
}
