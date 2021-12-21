using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D2DPolygonToTexture : DTexGenUnit<D2DPolygonToTexture.Data> {
    private static readonly int _Color = Shader.PropertyToID("_Color");
    private static readonly int _Matrix = Shader.PropertyToID("_Matrix");

    public struct Data {
      public DValue? Vertices;
      public DValue Position;
      public DValue Rotation;
      public DValue Scale;
      public DValue Color;
      public Color BackgroundColor;
    }

    [DoNotSerialize][NoEditor] public ValueInput Vertices;
    [DoNotSerialize][PortLabelHidden][Vector2][OneRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Scalar][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;
    [DoNotSerialize][PortLabelHidden][Vector2][ScaleRange] public ValueInput Scale;
    [DoNotSerialize][Color] public ValueInput Color;
    [DoNotSerialize][Color] public ValueInput BackgroundColor;

    private Vector3[] _vertexArray = {};
    private int[] _triangleArray = {};

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
        Position = flow.GetValue<DValue>(Position),
        Rotation = flow.GetValue<DValue>(Rotation),
        Scale = flow.GetValue<DValue>(Scale),
        Color = flow.GetValue<DValue>(Color),
        BackgroundColor = flow.GetValue<DValue>(BackgroundColor).ColorFromRow(0),
      };
    }

    protected override void Compute(Flow flow, Data data, RenderTexture output) {
      RenderTexture oldRenderTexture = RenderTexture.active;
      RenderTexture.active = output;

      GL.Clear(clearDepth: true, clearColor: true, backgroundColor: data.BackgroundColor);
      if (data.Vertices != null && data.Vertices.Value.Rows > 2) {
        if (_mesh == null) {
          _mesh = new Mesh();
        }

        DValue vertices = data.Vertices.Value;
        int vertexCount = vertices.Rows;
        int triangleCount = vertexCount - 2;

        Array.Resize(ref _vertexArray, vertexCount);
        Array.Resize(ref _triangleArray, triangleCount * 3);
        for (int i = 0; i < vertexCount; ++i) {
          _vertexArray[i] = vertices.Vector2FromRow(i);
        }
        for (int i = 0; i < triangleCount; ++i) {
          _triangleArray[i * 3 + 0] = 0;
          _triangleArray[i * 3 + 1] = i + 1;
          _triangleArray[i * 3 + 2] = i + 2;
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertexArray);
        _mesh.SetTriangles(_triangleArray, 0);
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
