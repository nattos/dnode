using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class RangeAttribute : Attribute {
    public double Min { get; private set; }
    public double Max { get; private set; }
    public double DefaultValue0 { get; private set; }
    public double DefaultValue1 { get; private set; }
    public double DefaultValue2 { get; private set; }
    public double DefaultValue3 { get; private set; }

    public RangeAttribute(double min, double max, double defaultValue0, double defaultValue1 = 0.0, double defaultValue2 = 0.0, double defaultValue3 = 0.0) {
      Min = min;
      Max = max;
      DefaultValue0 = defaultValue0;
      DefaultValue1 = defaultValue1;
      DefaultValue2 = defaultValue2;
      DefaultValue3 = defaultValue3;
    }
  }

  public class ExposureRangeAttribute : RangeAttribute {
    public ExposureRangeAttribute(double minEv = D3DConstants.DefaultMinEv, double maxEv = D3DConstants.DefaultMaxEv, double defaultValue = 0.0)
      : base(minEv, maxEv, defaultValue) {}
  }
}
