using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public class DataEdge {
    public DataPlug Source;
    public DataPlug Destination;

    public DataField? SourceFieldOrNull =>
        Source.Node.OutputSpec.Fields.FirstOrNull(field => field.Name == Source.FieldName);
    public DataField? DestinationFieldOrNull =>
        Destination.Node.InputSpec.Fields.FirstOrNull(field => field.Name == Destination.FieldName);

    public override string ToString() {
      return $"{Source} => {Destination}";
    }
  }
}
