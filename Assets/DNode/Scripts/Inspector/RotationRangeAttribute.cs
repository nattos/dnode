using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class RotationRangeAttribute : RangeAttribute {
    public RotationRangeAttribute(double defaultX = 0.0, double defaultY = 0.0, double defaultZ = 0.0)
      : base(-180.0, 180.0, defaultX, defaultY, defaultZ) {}
  }
}
