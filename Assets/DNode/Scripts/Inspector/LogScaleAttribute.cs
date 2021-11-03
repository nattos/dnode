using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class LogScaleAttribute : Attribute {
    public double ScalingFactor { get; private set; }

    public LogScaleAttribute(double scalingFactor = 1.0) {
      ScalingFactor = scalingFactor;
    }
  }
}
