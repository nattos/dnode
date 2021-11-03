using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  public enum ClampMode {
    None,
    Clamp,
    Wrap,
  }

  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class ClampModeAttribute : Attribute {
    public ClampMode ClampMode { get; private set; }

    public ClampModeAttribute(ClampMode clampMode = ClampMode.None) {
      ClampMode = clampMode;
    }
  }
}
