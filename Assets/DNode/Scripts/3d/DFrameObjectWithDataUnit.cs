using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DFrameObjectWithDataUnit<TData> : DFrameObjectUnit {
    protected override DFrameArray<DFrameObject> Compute(Flow flow, DFrameObject[] inputs) {
      TData data = GetData(flow, inputs);
      int row = 0;
      foreach (DFrameObject input in inputs) {
        ApplyToObject(data, row, input.GameObject);
        ++row;
      }
      return new DFrameArray<DFrameObject> { ValueArray = inputs };
    }

    protected abstract TData GetData(Flow flow, DFrameObject[] inputs);
    protected abstract void ApplyToObject(TData data, int row, GameObject input);
  }
}
