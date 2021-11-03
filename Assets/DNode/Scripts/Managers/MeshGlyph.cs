using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class MeshGlyph : MonoBehaviour {
    private Mesh _meshOverride;
    private bool _hasMeshOverride;
    private MeshGlyphFont _font;
    private int _codepoint = 0;

    public MeshGlyphFont Font {
      get => _font;
      set {
        if (_font == value) {
          return;
        }
        _font = value;
        UpdateGlyph();
      }
    }

    public int Codepoint {
      get {
        return _codepoint;
      }
      set {
        if (_codepoint == value) {
          return;
        }
        _codepoint = value;
        UpdateGlyph();
      }
    }

    public Mesh MeshOverride {
      get => _meshOverride;
      set {
        if (_meshOverride == value) {
          return;
        }
        _meshOverride = value;
        _hasMeshOverride = true;
        UpdateGlyph();
      }
    }

    private void ClearMeshOverride() {
      _hasMeshOverride = false;
    }

    private void UpdateGlyph() {
      GetComponent<MeshFilter>().mesh = _hasMeshOverride ? _meshOverride : DScriptMachine.CurrentInstance.MeshGlyphCache.GetMeshForCodePoint(_font, _codepoint);
    }
  }
}
