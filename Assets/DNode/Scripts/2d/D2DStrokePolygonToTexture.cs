using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D2DStrokePolygonToTexture : DTexGenUnit<D2DStrokePolygonToTexture.Data> {
    private static readonly int _Color = Shader.PropertyToID("_Color");
    private static readonly int _Matrix = Shader.PropertyToID("_Matrix");

    public struct Data {
      public DValue? Vertices;
      public DValue Width;
      public DValue MiterLimit;
      public DValue Position;
      public DValue Rotation;
      public DValue Scale;
      public DValue Color;
      public Color BackgroundColor;
    }

    [DoNotSerialize][NoEditor] public ValueInput Vertices;
    [DoNotSerialize][PortLabelHidden][Scalar][OneRange(0.05)][LogScale] public ValueInput Width;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0.0, 100.0, 4.0)][LogScale] public ValueInput MiterLimit;
    [DoNotSerialize][PortLabelHidden][Vector2][OneRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Scalar][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;
    [DoNotSerialize][PortLabelHidden][Vector2][ScaleRange] public ValueInput Scale;
    [DoNotSerialize][Color] public ValueInput Color;
    [DoNotSerialize][Color] public ValueInput BackgroundColor;

    private List<Vector3> _vertexArray = new List<Vector3>();
    private List<int> _triangleArray = new List<int>();

    private Mesh _mesh;
    private Material _material;

    public override void Dispose() {
      base.Dispose();
      Mesh mesh = _mesh;
      Material material = _material;
      DScriptMachine.DelayCall(() => {
        UnityUtils.Destroy(mesh);
        UnityUtils.Destroy(material);
      });
      _mesh = null;
      _material = null;
    }

    protected override void Definition() {
      base.Definition();

      Vertices = ValueInput<DEvent>(nameof(Vertices));
      Width = ValueInput<DValue>(nameof(Width), 0.05);
      MiterLimit = ValueInput<DValue>(nameof(MiterLimit), 4.0);
      Position = ValueInput<DValue>(nameof(Position), Vector2.zero);
      Rotation = ValueInput<DValue>(nameof(Rotation), 0.0f);
      Scale = ValueInput<DValue>(nameof(Scale), Vector2.one);
      Color = ValueInput<DValue>(nameof(Color), UnityEngine.Color.white);
      BackgroundColor = ValueInput<DValue>(nameof(BackgroundColor), UnityEngine.Color.clear);
    }

    protected override Texture GetBypassTexture(Flow flow) => Texture2D.blackTexture;
    
    protected override Data GetData(Flow flow) {
      return new Data {
        Vertices = GetNullableDValueFromDEventInput(flow, Vertices),
        Width = flow.GetValue<DValue>(Width),
        MiterLimit = flow.GetValue<DValue>(MiterLimit),
        Position = flow.GetValue<DValue>(Position),
        Rotation = flow.GetValue<DValue>(Rotation),
        Scale = flow.GetValue<DValue>(Scale),
        Color = flow.GetValue<DValue>(Color),
        BackgroundColor = flow.GetValue<DValue>(BackgroundColor).ColorFromRow(0),
      };
    }

    private int PushVertex(Vector2 v) {
      int index = _vertexArray.Count;
      _vertexArray.Add(v);
      return index;
    }

    private static Vector2? ComputeIntersection(Vector2 v1, Vector2 d1, Vector2 v2, Vector2 d2) {
      Vector2 xBasis = d2;
      Vector2 yBasis = new Vector2(-xBasis.y, xBasis.x);
      Vector2 v = new Vector2(Vector2.Dot(xBasis, v1 - v2), Vector2.Dot(yBasis, v1 - v2));
      Vector2 d = new Vector2(Vector2.Dot(xBasis, d1), Vector2.Dot(yBasis, d1));
      if (Mathf.Abs(d.y) < UnityUtils.DefaultFloatEpsilon) {
        return null;
      }
      float invSlope = d.x / d.y;
      float distanceAlong2 = v.x - v.y * invSlope;
      return v2 + d2 * distanceAlong2;
    }

    protected override void Compute(Flow flow, Data data, RenderTexture output) {
      RenderTexture oldRenderTexture = RenderTexture.active;
      RenderTexture.active = output;

      GL.Clear(clearDepth: true, clearColor: true, backgroundColor: data.BackgroundColor);
      if (data.Vertices != null && data.Vertices.Value.Rows > 1) {
        if (_mesh == null) {
          _mesh = new Mesh();
        }

        DValue vertices = data.Vertices.Value;
        int vertexCount = vertices.Rows;
        Vector2 vertexH1 = vertices.Vector2FromRow(0);
        Vector2 vertexH2 = vertices.Vector2FromRow(-1);
        int initialOuterVertex = default;
        int initialInnerVertex = default;
        int prevOuterVertex = default;
        int prevInnerVertex = default;

        _vertexArray.Clear();
        _triangleArray.Clear();
        for (int i = 0; i < vertexCount; ++i) {
          Vector2 vertexH0 = vertices.Vector2FromRow(i + 1);
          if ((vertexH0 - vertexH1).sqrMagnitude < UnityUtils.DefaultFloatEpsilon) {
            continue;
          }
          float width = data.Width.FloatFromRow(i + 1);
          float halfWidth = width * 0.5f;
          float miterLimit = data.MiterLimit.FloatFromRow(i + 1);
          float miterLengthLimit = halfWidth * miterLimit;

          Vector2 inVec = (vertexH1 - vertexH2).normalized;
          Vector2 outVec = (vertexH0 - vertexH1).normalized;
          Vector2 inNormal = new Vector2(-inVec.y, inVec.x);
          Vector2 outNormal = new Vector2(-outVec.y, outVec.x);

          Vector2 inOuterEndpoint = vertexH1 + inNormal * halfWidth;
          Vector2 outOuterEndpoint = vertexH1 + outNormal * halfWidth;
          Vector2 outerVertexPos = ComputeIntersection(inOuterEndpoint, inVec, outOuterEndpoint, -outVec) ?? outOuterEndpoint;
          Vector2 inInnerEndpoint = vertexH1 + inNormal * -halfWidth;
          Vector2 outInnerEndpoint = vertexH1 + outNormal * -halfWidth;
          Vector2 innerVertexPos = ComputeIntersection(inInnerEndpoint, inVec, outInnerEndpoint, -outVec) ?? outInnerEndpoint;

          int nextOuterVertex;
          int nextInnerVertex;
          int outerVertex;
          int innerVertex;
          float outerMiterLength = Vector2.Dot(outVec, outerVertexPos - outOuterEndpoint);
          float innerMiterLength = Vector2.Dot(outVec, innerVertexPos - outInnerEndpoint);
          if (outerMiterLength > miterLengthLimit) {
            nextOuterVertex = PushVertex(outOuterEndpoint);
            nextInnerVertex = PushVertex(outInnerEndpoint);
            outerVertexPos = inOuterEndpoint;
            innerVertexPos = inInnerEndpoint;
            outerVertex = PushVertex(outerVertexPos);
            innerVertex = PushVertex(innerVertexPos);

            int inMiterVertex = PushVertex(inInnerEndpoint + inVec * miterLengthLimit);
            int outMiterVertex = PushVertex(outInnerEndpoint - outVec * miterLengthLimit);
            _triangleArray.Add(outerVertex);
            _triangleArray.Add(innerVertex);
            _triangleArray.Add(outMiterVertex);
            _triangleArray.Add(outMiterVertex);
            _triangleArray.Add(innerVertex);
            _triangleArray.Add(inMiterVertex);
          } else if (innerMiterLength > miterLengthLimit) {
            nextOuterVertex = PushVertex(outOuterEndpoint);
            nextInnerVertex = PushVertex(outInnerEndpoint);
            outerVertexPos = inOuterEndpoint;
            innerVertexPos = inInnerEndpoint;
            outerVertex = PushVertex(outerVertexPos);
            innerVertex = PushVertex(innerVertexPos);

            int inMiterVertex = PushVertex(inOuterEndpoint + inVec * miterLengthLimit);
            int outMiterVertex = PushVertex(outOuterEndpoint - outVec * miterLengthLimit);
            _triangleArray.Add(outerVertex);
            _triangleArray.Add(innerVertex);
            _triangleArray.Add(inMiterVertex);
            _triangleArray.Add(inMiterVertex);
            _triangleArray.Add(innerVertex);
            _triangleArray.Add(outMiterVertex);
          } else {
            nextOuterVertex = outerVertex = PushVertex(outerVertexPos);
            nextInnerVertex = innerVertex = PushVertex(innerVertexPos);
          }

          if (i == 0) {
            initialOuterVertex = outerVertex;
            initialInnerVertex = innerVertex;
          } else {
            _triangleArray.Add(prevOuterVertex);
            _triangleArray.Add(prevInnerVertex);
            _triangleArray.Add(outerVertex);
            _triangleArray.Add(outerVertex);
            _triangleArray.Add(prevInnerVertex);
            _triangleArray.Add(innerVertex);
          }

          prevOuterVertex = nextOuterVertex;
          prevInnerVertex = nextInnerVertex;
          vertexH2 = vertexH1;
          vertexH1 = vertexH0;
        }
        _triangleArray.Add(prevOuterVertex);
        _triangleArray.Add(prevInnerVertex);
        _triangleArray.Add(initialOuterVertex);
        _triangleArray.Add(initialOuterVertex);
        _triangleArray.Add(prevInnerVertex);
        _triangleArray.Add(initialInnerVertex);

        _mesh.Clear();
        _mesh.SetVertices(_vertexArray, 0, _vertexArray.Count);
        _mesh.SetTriangles(_triangleArray, 0, _triangleArray.Count, 0);
        _vertexArray.Clear();
        _triangleArray.Clear();
      }
      if (_mesh != null) {
        if (_material == null) {
          _material = new Material(Shader.Find("Hidden/PolyFillSolid"));
        }
        Vector2 aspectRatioScale = new Vector2(output.height / (float)output.width, -1.0f);
        GL.PushMatrix();
        GL.LoadPixelMatrix();

        int instanceCount =
            Math.Max(data.Position.Rows,
            Math.Max(data.Rotation.Rows,
            Math.Max(data.Scale.Rows,
            data.Color.Rows)));
        for (int i = 0; i < instanceCount; ++i) {
          _material.SetColor(_Color, data.Color.ColorFromRow(i));
          _material.SetPass(0);
          Matrix4x4 localMat = Matrix4x4.TRS(
              Vector3.zero,
              Quaternion.AngleAxis(data.Rotation.FloatFromRow(i), Vector3.forward),
              data.Scale.Vector2FromRow(i));
          Matrix4x4 objectMat = Matrix4x4.TRS(
              data.Position.Vector2FromRow(i).ElementMul(aspectRatioScale),
              Quaternion.identity,
              aspectRatioScale);
          Matrix4x4 mat = objectMat * localMat;
          _material.SetMatrix(_Matrix, mat);
          Graphics.DrawMeshNow(_mesh, mat);
        }

        GL.PopMatrix();
      }

      RenderTexture.active = oldRenderTexture;
    }
  }
}
