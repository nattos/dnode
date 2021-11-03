using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class WorldRangeAttribute : RangeAttribute {
    public WorldRangeAttribute(double min = D3DConstants.DefaultWorldRangeMin, double max = D3DConstants.DefaultWorldRangeMax, double defaultX = 0.0, double defaultY = 0.0, double defaultZ = 0.0)
      : base(min, max, defaultX, defaultY, defaultZ) {}
  }
}
