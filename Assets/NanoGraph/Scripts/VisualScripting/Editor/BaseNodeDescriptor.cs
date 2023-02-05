using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace NanoGraph.VisualScripting {
  [Descriptor(typeof(BaseNode))]
  public class BaseNodeDescriptor : UnitDescriptor<BaseNode> {
    public readonly BaseNode Node;
    public BaseNodeDescriptor(BaseNode baseNode) : base(baseNode) {
      Node = baseNode;
    }

    protected override IEnumerable<EditorTexture> DefinedIcons() {
      return (Node as NodeBasedNode)?.Node?.Messages?.Count > 0 ? new[] { BoltCore.Icons.errorMessage } : Array.Empty<EditorTexture>();
    }
  }
}
