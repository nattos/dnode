using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  public class ZeroOneRangeAttribute : RangeAttribute {
    public ZeroOneRangeAttribute(double defaultValue = 0.0)
      : base(0.0, 1.0, defaultValue, defaultValue, defaultValue) {}
  }

  public class OneRangeAttribute : RangeAttribute {
    public OneRangeAttribute(double defaultValue = 0.0)
      : base(-1.0, 1.0, defaultValue, defaultValue, defaultValue) {}
  }
}
