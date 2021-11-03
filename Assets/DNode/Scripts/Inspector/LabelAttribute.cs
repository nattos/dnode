using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class LabelAttribute : Attribute {
    public string Label { get; private set; }

    public LabelAttribute(string label) {
      Label = label;
    }
  }
}
