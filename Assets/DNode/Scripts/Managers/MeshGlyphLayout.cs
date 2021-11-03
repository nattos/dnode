using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  public class MeshGlyphLayout {
    private static readonly char[] _capHeightChars = { 'H', 'M' };

    public struct Glyph {
      public int Codepoint;
      public float PositionMin;
      public float PositionMax;
      public float Width;
      public UnityEngine.Mesh Mesh;
    }

    public string Text {
      get {
        return _text;
      }
      set {
        if (_text == value) {
          return;
        }
        _text = value;
        _dirty = true;
      }
    }

    public MeshGlyphFont Font {
      get {
        return _font;
      }
      set {
        if (_font == value) {
          return;
        }
        _font = value;
        _dirty = true;
      }
    }

    public bool ForceMonospace {
      get {
        return _forceMonospace;
      }
      set {
        if (_forceMonospace == value) {
          return;
        }
        _forceMonospace = value;
        _dirty = true;
      }
    }

    public IReadOnlyList<Glyph> LayoutGlyphs { get { ValidateLayout(); return _glyphs; } }
    public float LayoutWidth { get { ValidateLayout(); return _totalWidth; } }
    public float LayoutAscent { get { ValidateLayout(); return _ascent; } }
    public float LayoutDescent { get { ValidateLayout(); return _descent; } }
    public float LayoutCapHeight { get { ValidateLayout(); return _capHeight; } }

    private string _text = "";
    private MeshGlyphFont _font = null;
    private bool _forceMonospace = false;
    private Glyph[] _glyphs = Array.Empty<Glyph>();
    private float _totalWidth = 0.0f;
    private float _descent = 0.0f;
    private float _ascent = 0.0f;
    private float _capHeight = 0.0f;
    private bool _dirty = false;

    private void ValidateLayout() {
      if (!_dirty) {
        return;
      }
      _dirty = false;
      UpdateGlyphs();
    }

    private void UpdateGlyphs() {
      byte[] codepointBytes = Encoding.UTF32.GetBytes(_text ?? "");
      int[] codepoints = new int[codepointBytes.Length / 4];
      for (int i = 0; i < codepoints.Length; ++i) {
        codepoints[i] = BitConverter.ToInt32(codepointBytes, i * 4);
      }
      Array.Resize(ref _glyphs, codepoints.Length);

      MeshGlyphFont font = _font ?? DScriptMachine.CurrentInstance.DefaultFont;
      MeshGlyphFont.FontFallback fontFallback = DScriptMachine.CurrentInstance.DefaultFontFallback;
      Typography.OpenFont.Typeface typeface = font.Typeface;
      float scalingFactor = 1.0f / typeface.UnitsPerEm;

      int xMax = 0;
      ushort previousGlyphIndex = 0;
      MeshGlyphFont previousGlyphFont = font;
      for (int i = 0; i < codepoints.Length; ++i) {
        int codepoint = codepoints[i];
        int nextCodepoint = (i + 1) < codepoints.Length ? codepoints[i + 1] : 0;
        float localScalingFactor = scalingFactor;
        MeshGlyphFont localFont = font;
        Typography.OpenFont.Typeface localTypeface = typeface;
        ushort glyphIndex = typeface.GetGlyphIndex(codepoint, nextCodepoint, out bool skipNextCodepoint);
        // Handle fallback fonts.
        if (glyphIndex == 0) {
          foreach (MeshGlyphFont fallbackFont in fontFallback.Fonts) {
            glyphIndex = fallbackFont.Typeface.GetGlyphIndex(codepoint, nextCodepoint, out skipNextCodepoint);
            if (glyphIndex != 0) {
              localFont = fallbackFont;
              localTypeface = fallbackFont.Typeface;
              localScalingFactor = 1.0f / localTypeface.UnitsPerEm;
              break;
            }
          }
        }
        // TODO: Handle skipNextCodepoint.

        int xBias;
        int advanceWidth;
        if (_forceMonospace) {
          xBias = 0;
          advanceWidth = localTypeface.UnitsPerEm;
        } else {
          advanceWidth = localTypeface.GetAdvanceWidthFromGlyphIndex(glyphIndex);
          int leftSideBearing = localTypeface.GetLeftSideBearing(glyphIndex);
          int kernDistance = previousGlyphFont == localFont ? localTypeface.GetKernDistance(previousGlyphIndex, glyphIndex) : 0;
          xBias = kernDistance - leftSideBearing;
        }
        int xPos = xMax + xBias;
        int xNextPos = xPos + advanceWidth;

        _glyphs[i] = new Glyph {
          Codepoint = codepoint,
          PositionMin = xPos * localScalingFactor,
          PositionMax = xNextPos * localScalingFactor,
          Width = advanceWidth * localScalingFactor,
          Mesh = DScriptMachine.CurrentInstance.MeshGlyphCache.GetMeshForCodePoint(localFont, codepoint),
        };

        previousGlyphIndex = glyphIndex;
        previousGlyphFont = localFont;
        xMax = xNextPos;
      }

      int capHeightMax = 0;
      foreach (char c in _capHeightChars) {
        ushort glyphIndex = typeface.GetGlyphIndex(c);
        if (glyphIndex == 0) {
          continue;
        }
        var glyph = typeface.GetGlyph(glyphIndex);
        capHeightMax = Math.Max(capHeightMax, glyph.Bounds.YMax);
      }
      int capHeight = capHeightMax >= 0 ? capHeightMax : typeface.Ascender;

      _ascent = typeface.Ascender * scalingFactor;
      _descent = typeface.Descender * scalingFactor;
      _capHeight = capHeight * scalingFactor;
      _totalWidth = xMax * scalingFactor;
    }

  }
}
