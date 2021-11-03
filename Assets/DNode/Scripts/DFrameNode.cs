using System;

namespace DNode {
  public class DFrameNode : IDisplayDataProvider {
    public virtual string ToShortString() => Unity.VisualScripting.StringUtility.Truncate(this.GetType().Name, 20);
    public Type DisplayIconAsType => typeof(UnityEngine.Object);
  }
}
