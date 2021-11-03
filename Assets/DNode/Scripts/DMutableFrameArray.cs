using System.Collections.Generic;
using System.Linq;

namespace DNode {
  public struct DMutableFrameArray<T> where T : DFrameNode {
    public T[] ValueArray;

    public int Length => ValueArray.Length;

    public DMutableFrameArray(int rows) {
      ValueArray = new T[rows];
    }

    public DMutableFrameArray(IEnumerable<T> input) {
      ValueArray = input.ToArray();
    }

    public T this[int i] {
      get {
        return ValueArray[i];
      }
      set {
        ValueArray[i] = value;
      }
    }

    public DFrameArray<T> ToValue() {
      return new DFrameArray<T> { ValueArray = ValueArray };
    }
  }
}
