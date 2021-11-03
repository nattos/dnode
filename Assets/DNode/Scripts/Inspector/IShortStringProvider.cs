using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  public interface IDisplayDataProvider {
    string ToShortString();
    Type DisplayIconAsType { get; }
  }
}
