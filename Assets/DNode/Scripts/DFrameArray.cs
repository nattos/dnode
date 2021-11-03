using System;
using System.Collections.Generic;

namespace DNode {
  public struct DFrameArray<T> : IDisplayDataProvider where T : DFrameNode {
    public T[] ValueArray;

    public static implicit operator DFrameArray<T>(T value) {
      return new DFrameArray<T> { ValueArray = new[] { value } };
    }

    public static implicit operator T(DFrameArray<T> value) {
      return value.ValueArray == null ? default : value.ValueArray[0];
    }

    public string ToShortString() {
      if (ValueArray == null || ValueArray.Length == 0) {
        return "<empty>";
      } else if (ValueArray.Length == 0) {
        return ValueArray[0].ToShortString();
      } else {
        return $"{ValueArray.Length} objs";
      }
    }

    public Type DisplayIconAsType => typeof(List<object>);
  }
}
