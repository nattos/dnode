using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexResize : DTexGenUnit<DTexResize.Data> {
    public struct Data {
      public Texture InputTexture;
      public Vector2Int Size;
      public Vector2 CroppingScale;
      public Vector2 CroppingAnchor;
      public Vector2 CroppingOffset;
      public bool RetainHeight;
      public bool RetainWidth;
      public DTexInterpolationMode Interpolate;
      public DTexWrapMode Wrap;
    }

    [DoNotSerialize][PortLabelHidden] public ValueInput Input;
    [DoNotSerialize][PortLabelHidden][Vector2][Range(1, 2048, 1920, 1080)][LogScale][ShortEditor] public ValueInput Size;
    [DoNotSerialize] public ValueInput RetainWidth;
    [DoNotSerialize] public ValueInput RetainHeight;
    [DoNotSerialize][PortLabelHidden][Vector2][Range(0, 16.0, 1.0, 1.0)][ShortEditor] public ValueInput CroppingScale;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange(0.5)][ShortEditor] public ValueInput CroppingAnchor;
    [DoNotSerialize][PortLabelHidden][Vector2][OneRange][ShortEditor] public ValueInput CroppingOffset;
    [DoNotSerialize] public ValueInput Interpolate;
    [DoNotSerialize] public ValueInput Wrap;

    private bool _cropToCropFactor = false;
    [Serialize][Inspectable] public bool CropToCropFactor {
      get => _cropToCropFactor;
      set {
        _cropToCropFactor = value;
        PortsChanged();
      }
    }
    [Inspectable] public bool RetainPixelAspect = false;

    public DTexResize() {
      SizeSource = TextureGenSizeSource.Auto;
    }

    protected override void Definition() {
      Input = ValueInput<DFrameTexture>(nameof(Input));
      base.Definition();
      if (!_cropToCropFactor) {
        Size = ValueInput<DValue>(nameof(Size), new Vector2(1920, 1080));
        RetainWidth = ValueInput<bool>(nameof(RetainWidth), false);
        RetainHeight = ValueInput<bool>(nameof(RetainHeight), false);
      }
      CroppingScale = ValueInput<DValue>(nameof(CroppingScale), Vector2.one);
      if (!_cropToCropFactor) {
        CroppingAnchor = ValueInput<DValue>(nameof(CroppingAnchor), Vector2.one * 0.5f);
      }
      CroppingOffset = ValueInput<DValue>(nameof(CroppingOffset), Vector2.zero);
      Interpolate = ValueInput<DTexInterpolationMode>(nameof(Interpolate), DTexInterpolationMode.Smooth);
      Wrap = ValueInput<DTexWrapMode>(nameof(Wrap), DTexWrapMode.Clamp);
    }

    protected override void Compute(Flow flow, Data data, RenderTexture output) {
      Texture texture = data.InputTexture;
      texture.wrapMode = data.Wrap == DTexWrapMode.Wrap ? TextureWrapMode.Mirror : TextureWrapMode.Clamp;
      texture.filterMode = data.Interpolate == DTexInterpolationMode.Hard ? FilterMode.Point : FilterMode.Bilinear;

      Vector2 scale;
      Vector2 offset;
      if (_cropToCropFactor) {
        scale = Vector2.one;
        offset = -data.CroppingOffset;
      } else {
        Vector2 croppingScale = data.CroppingScale;
        if (RetainPixelAspect) {
          croppingScale.x *= texture.width * (float)output.height / (output.width * (float)texture.height);
        }
        scale = Vector3.one.ElementDiv(Vector3.Max(Vector3.one * (float)UnityUtils.DefaultEpsilon, croppingScale));
        offset = -data.CroppingOffset.ElementMul(scale) - data.CroppingAnchor.ElementMul(scale - Vector2.one);
      }

      Graphics.Blit(texture, output, scale, offset);
    }

    protected override Data GetData(Flow flow) {
      return new Data {
        InputTexture = GetTextureInput(flow, Input, Texture2D.blackTexture),
        Size = _cropToCropFactor ? Vector2Int.one : Vector2Int.RoundToInt(flow.GetValue<Vector2>(Size)),
        RetainWidth = _cropToCropFactor ? false : flow.GetValue<bool>(RetainWidth),
        RetainHeight = _cropToCropFactor ? false : flow.GetValue<bool>(RetainHeight),
        CroppingScale = flow.GetValue<DValue>(CroppingScale),
        CroppingAnchor = _cropToCropFactor ? Vector2.zero : (Vector2)flow.GetValue<DValue>(CroppingAnchor),
        CroppingOffset = flow.GetValue<DValue>(CroppingOffset),
        Interpolate = flow.GetValue<DTexInterpolationMode>(Interpolate),
        Wrap = flow.GetValue<DTexWrapMode>(Wrap),
      };
    }

    protected override Texture GetBypassTexture(Flow flow) => GetTextureInput(flow, Input, Texture2D.whiteTexture);
    protected override Vector2Int? GetSourceSize(Data data) {
      if (_cropToCropFactor) {
        return Vector2Int.Max(Vector2Int.one, Vector2Int.RoundToInt(new Vector2(data.InputTexture.width, data.InputTexture.height).ElementMul(data.CroppingScale)));
      }
      Vector2Int size = data.Size;
      if (data.RetainWidth) {
        size.x = data.InputTexture.width;
      }
      if (data.RetainWidth) {
        size.y = data.InputTexture.height;
      }
      return size;
    }
  }
}
