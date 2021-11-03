using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace DNode {
  [Serializable]
  public class DCustomEditorField<TTarget, TData> {
    [DoNotSerialize] public TTarget Target;
    public TData Data;

    public static DCustomEditorField<TTarget, TData> FromUnit(TTarget target, TData data = default) {
      return new DCustomEditorField<TTarget, TData> { Target = target, Data = data };
    }

    static DCustomEditorField() {
      DValue.Init();
    }
  }
}
