using System;
using System.Collections;
using Unity.EditorCoroutines.Editor;

namespace NanoGraph {
  public static class EditorUtils {
    public static readonly object LiveForever = new object();

    public static event Action DelayCall {
      add {
        InvokeLater(value);
      }
      remove {
        throw new NotSupportedException();
      }
    }

    public static void InvokeLater(Action action) {
      IEnumerator Coroutine() {
        yield return new EditorWaitForSeconds(0.0f);
        action();
      }
      EditorCoroutineUtility.StartCoroutine(Coroutine(), LiveForever);
    }

    public static void StartCoroutine(IEnumerator coroutine) {
      EditorCoroutineUtility.StartCoroutine(coroutine, LiveForever);
    }
  }
}
