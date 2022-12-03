using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public class EditableAttributeAttribute : Attribute {
  }

  public struct EditableAttribute {
    public string Name;
    public Type Type;
    public Func<IEditableAttributeProvider, object> Getter;
    public Action<IEditableAttributeProvider, object> Setter;
  }

  public interface IEditableAttributeProvider {
    IReadOnlyList<EditableAttribute> EditableAttributes { get; }
  }
}
