using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DTexColorLUT : DTexUnaryBlitUnit {
    private int _LogLut3D = Shader.PropertyToID("_LogLut3D");
    private int _Alpha = Shader.PropertyToID("_Alpha");
    private int _LogLut3D_Params = Shader.PropertyToID("_LogLut3D_Params");

    private TextAsset _textAsset;
    private string _path;
    private Texture3D _texture;
    private bool _textureValid;

    protected new void OnDestroy() {
      base.OnDestroy();
      UnityUtils.Destroy(_texture);
      _texture = null;
    }

    [DoNotSerialize] public ValueInput CubeAsset;
    [DoNotSerialize] public ValueInput CubePath;
    [DoNotSerialize] public ValueInput LutTexture;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange][ShortEditor] public ValueInput Alpha;

    protected override void Definition() {
      base.Definition();
      LutTexture = ValueInput<Texture3D>("3D LUT", null);
      CubeAsset = ValueInput<TextAsset>("CubeAsset", null);
      CubePath = ValueInput<string>("CubePath", "");
      Alpha = ValueInput<DValue>("Alpha", 1.0);
    }

    protected override string ShaderPath => "Hidden/TexApplyLut";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      int width;
      Texture3D lutTexture = flow.GetValue<Texture3D>(LutTexture);
      if (lutTexture) {
        width = lutTexture.width;
      } else {
        TextAsset textAsset = flow.GetValue<TextAsset>(CubeAsset);
        if (textAsset != _textAsset) {
          _textAsset = textAsset;
          UnityUtils.Destroy(_texture);
          _texture = null;
        }
        if (!_textAsset) {
          string nextPath = flow.GetValue<string>(CubePath);
          if (_path != nextPath) {
            _path = nextPath;
            UnityUtils.Destroy(_texture);
            _texture = null;
          }
        }
        if (!_texture) {
          int size = 64;
          _texture = new Texture3D(size, size, size, TextureFormat.RGBA32, mipChain: false);

          Color32[] values;
          bool textureValid = false;
          if (_textAsset != null || !string.IsNullOrEmpty(_path)) {
            try {
              System.IO.Stream stream;
              if (_textAsset != null) {
                stream = new System.IO.MemoryStream(_textAsset.bytes);
              } else {
                stream = System.IO.File.OpenRead(_path);
              }
              using (stream) {
                values = CubeLutReader.Read(stream, 64);
              }
              _texture.SetPixels32(values);
              _texture.Apply();
              textureValid = true;
            } catch (System.IO.IOException) {
            }
          }
          _textureValid = textureValid;
        }
        width = _texture.width;
      }

      base.SetMaterialProperties(flow, material);
      material.SetFloat(_Alpha, (lutTexture || _textureValid) ? (float)flow.GetValue<DValue>(Alpha) : 0.0f);
      material.SetTexture(_LogLut3D, lutTexture.OrNull() ?? (_textureValid ? _texture : UnityUtils.BlankTexture3D));
      material.SetVector(_LogLut3D_Params, new Vector2(1.0f / width, width - 1.0f));
    }
  }
}
