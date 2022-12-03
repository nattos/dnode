using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public struct DataPlug {
    public IDataNode Node;
    public string FieldName;

    public override string ToString() {
      return $"{Node}.{FieldName}";
    }
  }
}
