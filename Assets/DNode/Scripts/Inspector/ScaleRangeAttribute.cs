using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class ScaleRangeAttribute : RangeAttribute {
    public ScaleRangeAttribute(double min = 0, double max = 10.0, double defaultValue = 1.0)
      : base(min, max, defaultValue, defaultValue, defaultValue) {}
  }
}
