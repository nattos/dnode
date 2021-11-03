using Unity.VisualScripting;

namespace DNode {
  public class DMultiInputUnit<T> : MultiInputUnit<T> {
    static DMultiInputUnit() {
      DValue.Init();
    }
  }
}
