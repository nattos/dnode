using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

      public static FontFallback CreateEmptyFallback(MeshGlyphCache cache) {
        return new FontFallback(cache, Array.Empty<MeshGlyphFont>());
      }

      public static async Task<FontFallback> CreateFallbackAsync(MeshGlyphCache cache, IReadOnlyList<ScriptType> scriptOrder, IReadOnlyDictionary<ScriptType, FontFamily> fontFamilies) {
        await MeshGlyphCache.LoadFontNameCache();
        return CreateFallbackImmediate(cache, scriptOrder, fontFamilies);
      }

      public static FontFallback CreateFallbackImmediate(MeshGlyphCache cache, IReadOnlyList<ScriptType> scriptOrder, IReadOnlyDictionary<ScriptType, FontFamily> fontFamilies) {
        List<MeshGlyphFont> fonts = new List<MeshGlyphFont>();
        HashSet<MeshGlyphFont> fontSet = new HashSet<MeshGlyphFont>();
        foreach (ScriptType scriptType in scriptOrder) {
          if (!fontFamilies.TryGetValue(scriptType, out var fontFamily)) {
            continue;
          }
          foreach (string fontName in fontFamily.FontNames) {
            MeshGlyphFont font = cache.GetFontByNameImmediate(fontName);
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
    private static readonly ThreadLocal<List<Vector3>> _drawMeshVertices = new ThreadLocal<List<Vector3>>(() => new List<Vector3>());
    private static readonly ThreadLocal<List<Vector3>> _drawMeshNormals = new ThreadLocal<List<Vector3>>(() => new List<Vector3>());
    private static readonly ThreadLocal<List<int>> _drawMeshTriangles = new ThreadLocal<List<int>>(() => new List<int>());

    private readonly Dictionary<string, MeshGlyphFont> _fontCache =
        new Dictionary<string, MeshGlyphFont>();
    private readonly Dictionary<(MeshGlyphFont, int), Mesh> _glyphMeshes =
        new Dictionary<(MeshGlyphFont, int), Mesh>();

    // Thread-safe.
    public MeshGlyphFont GetFont(string path) {
      bool found;
      MeshGlyphFont font;
      lock (_fontCache) {
        found = _fontCache.TryGetValue(path, out font);
      }
      if (!found) {
        // TODO: it's possible we load a font multiple times. Fix this.
        font = MeshGlyphFont.TryLoadFont(path);
        lock (_fontCache) {
          _fontCache[path] = font;
        }
      }
      return font;
    }

    // Thread-safe.
    public MeshGlyphFont GetFontByNameImmediate(string name) {
      if (!TryGetFontPathImmediate(name, out string path)) {
        return null;
      }
      return GetFont(path);
    }

    // Main thread only.
    public void PrewarmCache(MeshGlyphFont font) {
      if (font == null) {
        return;
      }
      List<int> codepointsToLoad = new List<int>();
      for (int i = 0; i < 256; ++i) {
        if (!_glyphMeshes.ContainsKey((font, i))) {
          codepointsToLoad.Add(i);
        }
      }
      if (codepointsToLoad.Count == 0) {
        return;
      }
      Task.Run(() => {
        foreach (int codepoint in codepointsToLoad) {
          var meshData = PolygonizeGlyph(font, codepoint);
          UnityEditor.EditorApplication.delayCall += () => {
            Mesh mesh = meshData?.CreateMesh();
            _glyphMeshes[(font, codepoint)] = mesh;
          };
        }
      });
    }

    // Main thread only.
    public Mesh GetMeshForCodePoint(MeshGlyphFont font, int codepoint) {
      if (font == null) {
        return null;
      }
      if (!_glyphMeshes.TryGetValue((font, codepoint), out Mesh mesh)) {
        var meshData = PolygonizeGlyph(font, codepoint);
        mesh = meshData?.CreateMesh();
        _glyphMeshes[(font, codepoint)] = mesh;
      }
      return mesh;
    }

    // Main thread only.
    public void Dispose() {
      foreach (Mesh mesh in _glyphMeshes.Values) {
        UnityUtils.Destroy(mesh);
      }
      lock (_fontCache) {
        _fontCache.Clear();
      }
      _glyphMeshes.Clear();
    }

    private struct MeshData {
      public string Name;
      public Vector3[] Vertices;
      public Vector3[] Normals;
      public int[] Triangles;

      public Mesh CreateMesh() {
        Mesh mesh = new Mesh();
        mesh.name = Name;
        mesh.vertices = Vertices;
        mesh.normals = Normals;
        mesh.triangles = Triangles;
        return mesh;
      }
    }

    private static MeshData? PolygonizeGlyph(MeshGlyphFont font, int codepoint) {
      var glyphIndex = font.Typeface.GetGlyphIndex(codepoint);
      if (glyphIndex == 0) {
        return null;
      }
      Typography.OpenFont.Glyph glyph = font.Typeface.GetGlyph(glyphIndex);
      if (glyph.GlyphPoints.Length <= 1) {
        return null;
      }

      float xBias = (glyph.Bounds.XMin + glyph.Bounds.XMax) * 0.5f * font.InternalUnitsScale;
      GlyphPolygon[] polys = new GlyphPolygonizer().GetContours(glyph, font.InternalUnitsScale, new Vector2(xBias, 0.0f));
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

      var drawMeshVertices = _drawMeshVertices.Value;
      var drawMeshNormals = _drawMeshNormals.Value;
      var drawMeshTriangles = _drawMeshTriangles.Value;
      drawMeshVertices.Clear();
      drawMeshNormals.Clear();
      drawMeshTriangles.Clear();
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
        int baseVertexIndex = drawMeshVertices.Count;
        foreach (var v in triMesh.Vertices) {
          drawMeshVertices.Add(new Vector3((float)v.x, (float)v.y, 0.0f));
          drawMeshNormals.Add(Vector3.forward);
        }
        foreach (var tri in triMesh.Triangles) {
          drawMeshTriangles.Add(tri.vertices[0].id + baseVertexIndex);
          drawMeshTriangles.Add(tri.vertices[1].id + baseVertexIndex);
          drawMeshTriangles.Add(tri.vertices[2].id + baseVertexIndex);
        }
        // Reverse faces.
        baseVertexIndex = drawMeshVertices.Count;
        foreach (var v in triMesh.Vertices) {
          drawMeshVertices.Add(new Vector3((float)v.x, (float)v.y, 0.0f));
          drawMeshNormals.Add(Vector3.back);
        }
        foreach (var tri in triMesh.Triangles) {
          drawMeshTriangles.Add(tri.vertices[0].id + baseVertexIndex);
          drawMeshTriangles.Add(tri.vertices[2].id + baseVertexIndex);
          drawMeshTriangles.Add(tri.vertices[1].id + baseVertexIndex);
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

      MeshData mesh = new MeshData {
        Name = System.Text.Encoding.UTF32.GetString(BitConverter.GetBytes(codepoint)),
        Vertices = drawMeshVertices.ToArray(),
        Normals = drawMeshNormals.ToArray(),
        Triangles = drawMeshTriangles.ToArray(),
      };
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
      private Vector2 _offset;
      private readonly List<GlyphPolygon> _contours = new List<GlyphPolygon>();

      public GlyphPolygon[] GetContours(Typography.OpenFont.Glyph glyph, float scale, Vector2 offset) {
        _scale = scale;
        _offset = offset;
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
        _contours[_contours.Count - 1].ConicTo(new Vector2(x2, y2) * _scale - _offset, new Vector2(x1, y1) * _scale - _offset);
      }

      void Typography.OpenFont.IGlyphTranslator.Curve4(float x1, float y1, float x2, float y2, float x3, float y3) {
        _contours[_contours.Count - 1].CubicTo(new Vector2(x3, y3) * _scale - _offset, new Vector2(x1, y1) * _scale - _offset, new Vector2(x2, y2) * _scale - _offset);
      }

      void Typography.OpenFont.IGlyphTranslator.LineTo(float x1, float y1) {
        _contours[_contours.Count - 1].AddPoint(new Vector2(x1, y1) * _scale - _offset);
      }

      void Typography.OpenFont.IGlyphTranslator.MoveTo(float x0, float y0) {
        _contours.Add(new GlyphPolygon(_bezierStepSize));
        _contours[_contours.Count - 1].AddPoint(new Vector2(x0, y0) * _scale - _offset);
      }
    }

    private static object _fontCacheLock = new object();
    private static IReadOnlyDictionary<string, string> _fontMap = new Dictionary<string, string>();
    private static string[] _allFontNames = Array.Empty<string>();
    private static readonly Lazy<Task> _fetchFontsTask = new Lazy<Task>(() => Task.Run(async () => await PopulateFontCache()));

    public static Task LoadFontNameCache() => _fetchFontsTask.Value;
    public static bool IsFontCacheLoaded => _fetchFontsTask.IsValueCreated && _fetchFontsTask.Value.IsCompleted;

    private class FontCacheSerializedFormat {
      [SerializeField]
      public SortedDictionary<string, string> FontMap;
    }

    public static void SerializeFontCache(Stream stream) {
      SortedDictionary<string, string> fontMap = new SortedDictionary<string, string>();
      lock (_fontCacheLock) {
        foreach (var entry in _fontMap) {
          fontMap[entry.Key] = entry.Value;
        }
      }
      FontCacheSerializedFormat data = new FontCacheSerializedFormat { FontMap = fontMap };
      string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
      using (var writer = new StreamWriter(stream)) {
        writer.Write(json);
      }
    }

    public static void LoadSerializedFontCache(Stream stream) {
      string json;
      using (var reader = new StreamReader(stream)) {
        json = reader.ReadToEnd();
      }
      FontCacheSerializedFormat data = Newtonsoft.Json.JsonConvert.DeserializeObject<FontCacheSerializedFormat>(json);
      var fontMap = data.FontMap?.ToDictionary(e => e.Key, e => e.Value) ?? new Dictionary<string, string>();
      string[] fonts = fontMap.Keys.ToArray();
      Array.Sort(fonts);
      lock (_fontCacheLock) {
        _fontMap = fontMap;
        _allFontNames = fonts;
      }
    }

    public static bool TryGetFontPathImmediate(string name, out string path) {
      lock (_fontCacheLock) {
        return _fontMap.TryGetValue(name, out path);
      }
    }

    public static string[] AllFontNamesImmediate {
      get {
        lock (_fontCacheLock) {
          return _allFontNames;
        }
      }
    }

    private async static Task PopulateFontCache() {
      var fontMap = await FetchFontNameToPathMap();
      string[] fonts = fontMap.Keys.ToArray();
      Array.Sort(fonts);
      lock (_fontCacheLock) {
        _fontMap = fontMap;
        _allFontNames = fonts;
      }
    }

    private async static Task<IReadOnlyDictionary<string, string>> FetchFontNameToPathMap() {
      // Delegate back to the main thread so we can call Unity.
      TaskCompletionSource<string[]> osFontPathAsync = new TaskCompletionSource<string[]>();
      UnityEditor.EditorApplication.delayCall += () => {
        try {
          osFontPathAsync.SetResult(Font.GetPathsToOSFonts());
        } catch (Exception e) {
          osFontPathAsync.SetException(e);
        }
      };
      string[] osFontPaths = await osFontPathAsync.Task;
      var fontEntries = osFontPaths.SelectMany(path => {
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
