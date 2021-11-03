using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

namespace DNode {
  public class MeshGlyphFont {
    public enum ScriptType {
      Latin,
      Arabic,
      Japanese,
      Korean,
      Russian,
      Chinese,
    }

    public class FontFamily {
      public readonly IReadOnlyList<string> FontNames;

      public FontFamily(IReadOnlyList<string> fontNames) {
        FontNames = fontNames;
      }
    }

    public class FontFallback {
      public readonly MeshGlyphCache Cache;
      public readonly IReadOnlyList<MeshGlyphFont> Fonts;

      public FontFallback(MeshGlyphCache cache, IReadOnlyList<MeshGlyphFont> fonts) {
        Cache = cache;
        Fonts = fonts;
      }

      public void PrewarmCache() {
        foreach (MeshGlyphFont font in Fonts) {
          Cache.PrewarmCache(font);
        }
      }

      public static FontFallback CreateFallback(MeshGlyphCache cache, IReadOnlyList<ScriptType> scriptOrder, IReadOnlyDictionary<ScriptType, FontFamily> fontFamilies) {
        List<MeshGlyphFont> fonts = new List<MeshGlyphFont>();
        HashSet<MeshGlyphFont> fontSet = new HashSet<MeshGlyphFont>();
        foreach (ScriptType scriptType in scriptOrder) {
          if (!fontFamilies.TryGetValue(scriptType, out var fontFamily)) {
            continue;
          }
          foreach (string fontName in fontFamily.FontNames) {
            MeshGlyphFont font = cache.GetFontByName(fontName);
            if (font == null) {
              continue;
            }
            if (!fontSet.Contains(font)) {
              fonts.Add(font);
              fontSet.Add(font);
            }
            break;
          }
        }
        return new FontFallback(cache, fonts);
      }
    }

    public readonly string Path;
    public readonly Typography.OpenFont.Typeface Typeface;
    public readonly float InternalUnitsScale;

    private MeshGlyphFont(string path, Typography.OpenFont.Typeface typeface) {
      Path = path;
      Typeface = typeface;
      InternalUnitsScale = 1.0f / typeface.UnitsPerEm;
    }

    public static MeshGlyphFont TryLoadFont(string path) {
      try {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read)) {
          var reader = new Typography.OpenFont.OpenFontReader();
          var preview = reader.ReadNamePreview(stream);
          Typography.OpenFont.Typeface typeface;
          if (preview.IsFontCollection) {
            typeface = reader.Read(stream, preview.GetMember(0).ActualStreamOffset);
          } else {
            stream.Seek(0, SeekOrigin.Begin);
            typeface = reader.Read(stream);
          }
          return new MeshGlyphFont(path, typeface);
        }
      } catch (IOException) {
        return null;
      }
    }
  }

  public class MeshGlyphCache : IDisposable {
    private static readonly List<Vector3> _drawMeshVertices = new List<Vector3>();
    private static readonly List<Vector3> _drawMeshNormals = new List<Vector3>();
    private static readonly List<int> _drawMeshTriangles = new List<int>();

    private readonly Dictionary<string, MeshGlyphFont> _fontCache =
        new Dictionary<string, MeshGlyphFont>();
    private readonly Dictionary<(MeshGlyphFont, int), Mesh> _glyphMeshes =
        new Dictionary<(MeshGlyphFont, int), Mesh>();

    public MeshGlyphFont GetFont(string path) {
      if (!_fontCache.TryGetValue(path, out MeshGlyphFont font)) {
        font = MeshGlyphFont.TryLoadFont(path);
        _fontCache[path] = font;
      }
      return font;
    }

    public MeshGlyphFont GetFontByName(string name) {
      if (!TryGetFontPath(name, out string path)) {
        return null;
      }
      return GetFont(path);
    }

    public void PrewarmCache(MeshGlyphFont font) {
      if (font == null) {
        return;
      }
      for (int i = 0; i < 256; ++i) {
        GetMeshForCodePoint(font, i);
      }
    }

    public Mesh GetMeshForCodePoint(MeshGlyphFont font, int codepoint) {
      if (font == null) {
        return null;
      }
      if (!_glyphMeshes.TryGetValue((font, codepoint), out Mesh mesh)) {
        mesh = PolygonizeGlyph(font, codepoint);
        _glyphMeshes[(font, codepoint)] = mesh;
      }
      return mesh;
    }

    public void Dispose() {
      foreach (Mesh mesh in _glyphMeshes.Values) {
        UnityUtils.Destroy(mesh);
      }
      _fontCache.Clear();
      _glyphMeshes.Clear();
    }

    private static Mesh PolygonizeGlyph(MeshGlyphFont font, int codepoint) {
      var glyphIndex = font.Typeface.GetGlyphIndex(codepoint);
      if (glyphIndex == 0) {
        return null;
      }
      Typography.OpenFont.Glyph glyph = font.Typeface.GetGlyph(glyphIndex);
      if (glyph.GlyphPoints.Length <= 1) {
        return null;
      }

      GlyphPolygon[] polys = new GlyphPolygonizer().GetContours(glyph, font.InternalUnitsScale);
      Array.Sort(polys, (a, b) => (int)Mathf.Sign(Mathf.Abs(b.Area) - Mathf.Abs(a.Area)));
      // Classify contours to find holes and their 'parents'.
      Dictionary<GlyphPolygon, List<GlyphPolygon>> childMap = polys.ToDictionary(p => p, p => new List<GlyphPolygon>());
      List<GlyphPolygon> roots = new List<GlyphPolygon>();
      for (int i = 0; i < polys.Length; ++i) {
        GlyphPolygon parent = null;
        for (int j = i - 1; j >= 0; --j) {
          // A contour is a hole if it is inside its parent and has different winding.
          if (polys[j].IsInside(polys[i].Points[0]) && polys[i].Area * polys[j].Area < 0) {
            parent = polys[j];
            break;
          }
        }
        if (parent != null) {
          childMap[parent].Add(polys[i]);
        } else {
          roots.Add(polys[i]);
        }
      }

      _drawMeshVertices.Clear();
      _drawMeshNormals.Clear();
      _drawMeshTriangles.Clear();
      void TriangulatePoly(GlyphPolygon poly) {
        List<GlyphPolygon> children = childMap[poly];

        if (poly.Points.Count <= 1) {
          return;
        }
        TriangleNet.Geometry.Polygon triangulationPoly = new TriangleNet.Geometry.Polygon();
        triangulationPoly.Add(new TriangleNet.Geometry.Contour(poly.Points.Select(p => new TriangleNet.Geometry.Vertex(p.x, p.y))), hole: false);
        foreach (GlyphPolygon child in children) {
          if (child.Points.Count <= 1) {
            continue;
          }
          triangulationPoly.Add(new TriangleNet.Geometry.Contour(child.Points.Select(p => new TriangleNet.Geometry.Vertex(p.x, p.y))), hole: true);
        }
        TriangleNet.Meshing.IMesh triMesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(triangulationPoly);

        // Forward faces.
        int baseVertexIndex = _drawMeshVertices.Count;
        foreach (var v in triMesh.Vertices) {
          _drawMeshVertices.Add(new Vector3((float)v.x, (float)v.y, 0.0f));
          _drawMeshNormals.Add(Vector3.forward);
        }
        foreach (var tri in triMesh.Triangles) {
          _drawMeshTriangles.Add(tri.vertices[0].id + baseVertexIndex);
          _drawMeshTriangles.Add(tri.vertices[1].id + baseVertexIndex);
          _drawMeshTriangles.Add(tri.vertices[2].id + baseVertexIndex);
        }
        // Reverse faces.
        baseVertexIndex = _drawMeshVertices.Count;
        foreach (var v in triMesh.Vertices) {
          _drawMeshVertices.Add(new Vector3((float)v.x, (float)v.y, 0.0f));
          _drawMeshNormals.Add(Vector3.back);
        }
        foreach (var tri in triMesh.Triangles) {
          _drawMeshTriangles.Add(tri.vertices[0].id + baseVertexIndex);
          _drawMeshTriangles.Add(tri.vertices[2].id + baseVertexIndex);
          _drawMeshTriangles.Add(tri.vertices[1].id + baseVertexIndex);
        }

        // Recurse into children of holes, which are filled.
        foreach (GlyphPolygon child in children) {
          List<GlyphPolygon> childChildren = childMap[child];
          foreach (GlyphPolygon childChild in childChildren) {
            TriangulatePoly(childChild);
          }
        }
      }
      foreach (GlyphPolygon root in roots) {
        TriangulatePoly(root);
      }

      Mesh mesh = new Mesh();
      mesh.name = System.Text.Encoding.UTF32.GetString(BitConverter.GetBytes(codepoint));
      mesh.vertices = _drawMeshVertices.ToArray();
      mesh.normals = _drawMeshNormals.ToArray();
      mesh.triangles = _drawMeshTriangles.ToArray();
      return mesh;
    }

    private class GlyphPolygon {
      private const int _maxBezierSteps = 10;
      private const float _epsilon = 1e-6f;

      private readonly float _bezierStepSize;
      private readonly List<Vector2> _points = new List<Vector2>();
      private float _area;

      public GlyphPolygon(float bezierStepSize) {
        _bezierStepSize = bezierStepSize;
      }

      public float Area => _area;
      public IReadOnlyList<Vector2> Points => _points;

      public void AddPoint(Vector2 p) {
        _points.Add(p);
      }

      public void Close() {
        Vector2 cur = _points[_points.Count - 1];
        _area = 0.0f;
        foreach (Vector2 next in _points) {
          _area += 0.5f * Cross(cur, next);
          cur = next;
        }
      }

      public void ConicTo(Vector2 p, Vector2 p1) {
        Vector2 p0 = _points[_points.Count - 1];
        float dist = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p);
        int steps = Mathf.Max(2, Mathf.Min(_maxBezierSteps, Mathf.FloorToInt(dist / _bezierStepSize)));
        for (int i = 1; i <= steps; ++i) {
          float t = i / (float)steps;
          _points.Add(Lerp(Lerp(p0, p1, t), Lerp(p1, p, t), t));
        }
      }

      public void CubicTo(Vector2 p, Vector2 p1, Vector2 p2) {
        Vector2 p0 = _points[_points.Count - 1];
        float dist = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p);
        int steps = Mathf.Max(2, Mathf.Min(_maxBezierSteps, Mathf.FloorToInt(dist / _bezierStepSize)));
        for (int i = 1; i <= steps; ++i) {
          float t = i / (float)steps;
          Vector2 a = Lerp(Lerp(p0, p1, t), Lerp(p1, p2, t), t);
          Vector2 b = Lerp(Lerp(p1, p2, t), Lerp(p2, p, t), t);
          _points.Add(Lerp(a, b, t));
        }
      }

      public bool IsInside(Vector2 p) {
        int count = 0;
        Vector2 cur = _points[_points.Count - 1];
        foreach (Vector2 next in _points) {
          Vector2 p0 = (cur.y < next.y ? cur : next);
          Vector2 p1 = (cur.y < next.y ? next : cur);
          if (p0.y < p.y + _epsilon && p1.y > p.y + _epsilon) {
            if ((p1.x - p0.x) * (p.y - p0.y) > (p.x - p0.x) * (p1.y - p0.y)) {
              count += 1;
            }
          }
          cur = next;
        }
        return (count % 2) != 0;
      }

      private static Vector2 Lerp(Vector2 a, Vector2 b, float t) {
        return a * (1.0f - t) + b * t;
      }
  
      private static float Cross(Vector2 p1, Vector2 p2) {
        return p1.x * p2.y - p1.y * p2.x;
      }
    }

    private class GlyphPolygonizer : Typography.OpenFont.IGlyphTranslator {
      private const float _bezierStepSize = 0.0015f;
      private float _scale = 1.0f;
      private readonly List<GlyphPolygon> _contours = new List<GlyphPolygon>();

      public GlyphPolygon[] GetContours(Typography.OpenFont.Glyph glyph, float scale) {
        _scale = scale;
        _contours.Clear();
        Typography.OpenFont.IGlyphReaderExtensions.Read(this, glyph.GlyphPoints, glyph.EndPoints);
        return _contours.ToArray();
      }

      void Typography.OpenFont.IGlyphTranslator.BeginRead(int contourCount) {}

      void Typography.OpenFont.IGlyphTranslator.EndRead() {}

      void Typography.OpenFont.IGlyphTranslator.CloseContour() {
        _contours[_contours.Count - 1].Close();
      }

      void Typography.OpenFont.IGlyphTranslator.Curve3(float x1, float y1, float x2, float y2) {
        _contours[_contours.Count - 1].ConicTo(new Vector2(x2, y2) * _scale, new Vector2(x1, y1) * _scale);
      }

      void Typography.OpenFont.IGlyphTranslator.Curve4(float x1, float y1, float x2, float y2, float x3, float y3) {
        _contours[_contours.Count - 1].CubicTo(new Vector2(x3, y3) * _scale, new Vector2(x1, y1) * _scale, new Vector2(x2, y2) * _scale);
      }

      void Typography.OpenFont.IGlyphTranslator.LineTo(float x1, float y1) {
        _contours[_contours.Count - 1].AddPoint(new Vector2(x1, y1) * _scale);
      }

      void Typography.OpenFont.IGlyphTranslator.MoveTo(float x0, float y0) {
        _contours.Add(new GlyphPolygon(_bezierStepSize));
        _contours[_contours.Count - 1].AddPoint(new Vector2(x0, y0) * _scale);
      }
    }

    private static readonly Lazy<IReadOnlyDictionary<string, string>> _fontMap = new Lazy<IReadOnlyDictionary<string, string>>(FetchFontNameToPathMap);

    public static bool TryGetFontPath(string name, out string path) {
      return _fontMap.Value.TryGetValue(name, out path);
    }

    public static string[] AllFontNames {
      get {
        string[] fonts = _fontMap.Value.Keys.ToArray();
        Array.Sort(fonts);
        return fonts;
      }
    }

    private static IReadOnlyDictionary<string, string> FetchFontNameToPathMap() {
      var fontEntries = Font.GetPathsToOSFonts().SelectMany(path => {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read)) {
          var fontReader = new Typography.OpenFont.OpenFontReader();
          var preview = fontReader.ReadNamePreview(stream);
          Typography.OpenFont.PreviewFontInfo[] members;
          if (preview.IsFontCollection) {
            members = Enumerable.Range(0, preview.MemberCount).Select(i => preview.GetMember(i)).ToArray();
          } else {
            members = new[] { preview };
          }
          return members.Select(member => new { Name = member.NameEntry.FullFontName.Trim(), Path = path });
        }
      }).ToArray();

      Dictionary<string, string> fontMap = new Dictionary<string, string>();
      foreach (var entry in fontEntries) {
        if (fontMap.ContainsKey(entry.Name)) {
          continue;
        }
        fontMap[entry.Name] = entry.Path;
      }
      return fontMap;
    }
  }
}
