using Unity.VisualScripting;

namespace DNode {
  public interface IDOutputNode {
    void ComputeFromFlow(Flow flow);
  }
}
