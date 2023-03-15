using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  internal static class UtilExtensions {
    public static T? ElementAtOrNull<T>(this IReadOnlyList<T> self, int index) where T : struct {
      if (index < 0 || index >= self.Count) {
        return null;
      }
      return self[index];
    }

    public static T? FirstOrNull<T>(this IEnumerable<T> self) where T : struct {
      foreach (T value in self) {
        return value;
      }
      return null;
    }

    public static T? FirstOrNull<T>(this IEnumerable<T> self, Predicate<T> predicate) where T : struct {
      foreach (T value in self) {
        if (predicate.Invoke(value)) {
          return value;
        }
      }
      return null;
    }

    public static bool TryGetRemove<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey key, out TValue value) {
      if (self.TryGetValue(key, out value)) {
        self.Remove(key);
        return true;
      }
      return false;
    }

    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> self) {
      return new Dictionary<TKey, TValue>(self);
    }

    public static TValue GetOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> self, TKey key, TValue defaultValue = default) {
      if (self.TryGetValue(key, out TValue value)) {
        return value;
      }
      return defaultValue;
    }

    public static TValue? GetOrNull<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> self, TKey key) where TValue : struct {
      if (self.TryGetValue(key, out TValue value)) {
        return value;
      }
      return null;
    }
  }
}
