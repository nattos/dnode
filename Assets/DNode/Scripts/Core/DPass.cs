using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace DNode {
  public class DPass : Unit {
    [DoNotSerialize][PortLabelHidden] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden] public ValueOutput result;
    protected override void Definition() {
      Input = ValueInput<object>("Input");
      result = ValueOutput<object>("result", flow => DNodeUtils.GetOptional<object>(flow, Input));
    }
  }
}
