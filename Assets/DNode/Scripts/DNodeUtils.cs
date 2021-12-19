using System;
using System.Linq;
using Unity.VisualScripting;

namespace DNode {
  public static class DNodeUtils {
    public static T GetOptional<T>(Flow flow, ValueInput input, T defaultValue = default) {
      if (!input.hasAnyConnection) {
        return defaultValue;
      }
      return flow.GetValue<T>(input);
    }

    public static Func<Flow, T> CachePerFrame<T>(Func<Flow, T> producer) {
      int currentFrameNumber = -1;
      T cachedValue = default;
      return flow => {
        int frameNumber = DScriptMachine.CurrentInstance.Transport.AbsoluteFrame;
        if (currentFrameNumber != frameNumber) {
          currentFrameNumber = frameNumber;
          cachedValue = producer.Invoke(flow);
        }
        return cachedValue;
      };
    }
  }
}