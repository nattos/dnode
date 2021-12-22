using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexFromValues : DTexGenUnit<DTexFromValues.Data> {
    public struct Data {
      public DValue Values;
      public DTexInterpolationMode Interpolate;
      public DTexWrapMode Wrap;
      public bool Transpose;
      public bool Flip;
      public bool FlipX;
      public bool FlipY;
      public bool Grayscale;
    }

    [DoNotSerialize][NoEditor] public ValueInput Values;
    [DoNotSerialize] public ValueInput Transpose;
    [DoNotSerialize] public ValueInput Interpolate;
    [DoNotSerialize] public ValueInput Wrap;
    [DoNotSerialize] public ValueInput Flip;
    [DoNotSerialize] public ValueInput FlipX;
    [DoNotSerialize] public ValueInput FlipY;

    private bool _inputIsGrayscale2D = false;
    [Serialize][Inspectable] public bool InputIsGrayscale2D {
      get => _inputIsGrayscale2D;
      set {
        _inputIsGrayscale2D = value;
        PortsChanged();
      }
    }

    private Color[] _colors = Array.Empty<Color>();
    private Texture2D _texture;

    public DTexFromValues() {
      SizeSource = TextureGenSizeSource.Auto;
    }

    public override void Dispose() {
      base.Dispose();
      DScriptMachine.DelayCall(() => {
        UnityUtils.Destroy(_texture);
      });
    }

    protected override void Definition() {
      base.Definition();

      Values = ValueInput<DValue>(nameof(Values));
      Transpose = ValueInput<bool>(nameof(Transpose), false);
      Interpolate = ValueInput<DTexInterpolationMode>(nameof(Interpolate), DTexInterpolationMode.Smooth);
      Wrap = ValueInput<DTexWrapMode>(nameof(Wrap), DTexWrapMode.Clamp);
      if (_inputIsGrayscale2D) {
        FlipX = ValueInput<bool>(nameof(FlipX), false);
        FlipY = ValueInput<bool>(nameof(FlipY), false);
      } else {
        Flip = ValueInput<bool>(nameof(Flip), false);
      }
    }

    protected override void Compute(Flow flow, Data data, RenderTexture output) {
      DValue values = data.Values;
      bool transpose = data.Transpose;
      int cols = Math.Max(1, values.Columns);
      int rows = Math.Max(1, values.Rows);
      int outputColumns = data.Grayscale ? cols : 1;
      int actualCols = transpose ? rows : outputColumns;
      int actualRows = transpose ? outputColumns : rows;
      int pixelCount = actualCols * actualRows;
      if (_texture == null || _texture.width != actualCols || _texture.height != actualRows) {
        _texture = new Texture2D(actualCols, actualRows, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
        Array.Resize(ref _colors, pixelCount);
      }

      if (data.Grayscale) {
        bool flipX = data.FlipX;
        bool flipY = data.FlipY;
        for (int row = 0; row < rows; ++row) {
          for (int col = 0; col < cols; ++col) {
            float value = (float)values[row, col];

            int outputCol = transpose ? row : col;
            int outputRow = transpose ? col : row;
            if (flipX) {
              outputCol = actualCols - outputCol - 1;
            }
            if (flipY) {
              outputRow = actualRows - outputRow - 1;
            }
            _colors[outputCol + outputRow * actualCols] = new Color(value, value, value, 1.0f);
          }
        }
      } else {
        bool flip = data.Flip;
        int index = 0;
        for (int row = 0; row < rows; ++row) {
          _colors[index++] = values.ColorFromRow(flip ? (rows - row - 1) : row, Color.black);
        }
      }
      _texture.SetPixels(_colors);
      _texture.Apply(updateMipmaps: false);
      _texture.wrapMode = data.Wrap == DTexWrapMode.Wrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
      _texture.filterMode = data.Interpolate == DTexInterpolationMode.Hard ? FilterMode.Point : FilterMode.Bilinear;

      Graphics.Blit(_texture, output);
    }

    protected override Data GetData(Flow flow) {
      return new Data {
        Values = DNodeUtils.GetOptional<DValue>(flow, Values, default),
        Transpose = flow.GetValue<bool>(Transpose),
        Interpolate = flow.GetValue<DTexInterpolationMode>(Interpolate),
        Wrap = flow.GetValue<DTexWrapMode>(Wrap),
        Flip = _inputIsGrayscale2D ? false : flow.GetValue<bool>(Flip),
        FlipX = _inputIsGrayscale2D ? flow.GetValue<bool>(FlipX) : false,
        FlipY = _inputIsGrayscale2D ? flow.GetValue<bool>(FlipY) : false,
        Grayscale = _inputIsGrayscale2D,
      };
    }

    protected override Vector2Int? GetSourceSize(Data data) {
      if (data.Grayscale) {
        if (data.Transpose) {
          return new Vector2Int(data.Values.Rows, data.Values.Columns);
        } else {
          return new Vector2Int(data.Values.Columns, data.Values.Rows);
        }
      } else {
        if (data.Transpose) {
          return new Vector2Int(data.Values.Rows, 1);
        } else {
          return new Vector2Int(1, data.Values.Rows);
        }
      }
    }
  }
}
