using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;

namespace DNode {
  [TypeIcon(typeof(object))]
  public struct DFrameNodes : IDisplayDataProvider {
    public IReadOnlyList<DFrameNode> Nodes;

    public static implicit operator DFrameNodes(DFrameArray<DFrameNode> value) {
      return new DFrameNodes { Nodes = value.ValueArray };
    }

    public static implicit operator DFrameNodes(DFrameArray<DFrameObject> value) {
      return new DFrameNodes { Nodes = value.ValueArray };
    }

    public static implicit operator DFrameNodes(DFrameArray<DFrameTexture> value) {
      return new DFrameNodes { Nodes = value.ValueArray };
    }

    public static implicit operator DFrameNodes(DFrameCommand value) {
      return new DFrameNodes { Nodes = new[] { value } };
    }

    public static implicit operator DFrameNodes(DFrameTexture value) {
      return new DFrameNodes { Nodes = new[] { value } };
    }

    public static implicit operator DFrameNodes(Texture value) {
      return new DFrameNodes { Nodes = new[] { (DFrameTexture)value } };
    }

    public string ToShortString() {
      if (Nodes == null || Nodes.Count == 0) {
        return "<empty>";
      } else if (Nodes.Count == 0) {
        return Nodes[0].ToShortString();
      } else {
        return $"{Nodes.Count} nodes";
      }
    }

    public Type DisplayIconAsType => typeof(List<object>);
  }
}
