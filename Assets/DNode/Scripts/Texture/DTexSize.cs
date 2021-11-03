using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexSize : DTexUnit {
    [DoNotSerialize][PortLabelHidden] public ValueInput Input;

    [DoNotSerialize]
    public ValueOutput resultHeight;
    [DoNotSerialize]
    public ValueOutput resultWidth;
    [DoNotSerialize]
    public ValueOutput resultVector;

    private bool _asVector = false;
    [Serialize][Inspectable] public bool AsVector {
      get => _asVector;
      set {
        _asVector = value;
        PortsChanged();
      }
    }

    protected override void Definition() {
      Input = ValueInput<DFrameTexture>("Input");

      Vector2Int ComputeFromFlow(Flow flow) {
        Texture texture = GetTextureInput(flow, Input, Texture2D.blackTexture);
        return new Vector2Int(texture.width, texture.height);
      }
      var resultFunc = DNodeUtils.CachePerFrame(ComputeFromFlow);
      if (!_asVector) {
        resultWidth = ValueOutput<int>("Width", flow => resultFunc(flow).x);
        resultHeight = ValueOutput<int>("Height", flow => resultFunc(flow).y);
      } else {
        resultVector = ValueOutput<DValue>("Vector", flow => {
          Vector2Int value = resultFunc(flow);
          DMutableValue outValue = new DMutableValue(1, 2);
          outValue[0, 0] = value.x;
          outValue[0, 1] = value.y;
          return outValue.ToValue();
        });
      }
    }
  }
}
