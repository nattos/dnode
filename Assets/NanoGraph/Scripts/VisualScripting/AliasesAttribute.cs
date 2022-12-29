using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace NanoGraph.VisualScripting {
  public interface IAliasProvider {
    IEnumerable<string> GetAliases();
    IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> GetAlternatives();
  }

  public class AliasAttribute : Attribute {
    public Type AliasType;

    public AliasAttribute(Type aliasType) {
      AliasType = aliasType;
    }
  }
}
