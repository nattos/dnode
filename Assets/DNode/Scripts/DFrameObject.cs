using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  [TypeIcon(typeof(GameObject))]
  public class DFrameObject : DFrameNode {
    public GameObject GameObject;

    public override string ToShortString() => GameObject == null ? "Null" : Unity.VisualScripting.StringUtility.Truncate(GameObject.name, 20);
  }
}
