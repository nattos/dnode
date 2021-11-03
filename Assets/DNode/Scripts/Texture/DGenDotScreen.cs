using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DGenDotScreen : DTexGenBlitUnit {
		private const string SHAPE_CIRCLE = "SHAPE_CIRCLE";
    private const string SHAPE_CIRCLE_SQUIRCLE = "SHAPE_CIRCLE_SQUIRCLE";
    private const string SHAPE_SQUIRCLE = "SHAPE_SQUIRCLE";
    private const string SHAPE_SQUIRCLE_SQUARE = "SHAPE_SQUIRCLE_SQUARE";
    private const string SHAPE_SQUARE = "SHAPE_SQUARE";

    private int _DotParams = Shader.PropertyToID("_DotParams");
    private int _ShapeParams = Shader.PropertyToID("_ShapeParams");
    private int _PositionParams = Shader.PropertyToID("_PositionParams");
    private int _LimitParams = Shader.PropertyToID("_LimitParams");
    private int _BaseColorHSL = Shader.PropertyToID("_BaseColorHSL");
    private int _ColorStepHSL = Shader.PropertyToID("_ColorStepHSL");
    private int _BackgroundColor = Shader.PropertyToID("_BackgroundColor");

    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange(0.5)] public ValueInput CellSize;
    [DoNotSerialize][PortLabelHidden][Vector2][Integer][Range(-1, 16, -1, -1)] public ValueInput LimitMax;
    [DoNotSerialize][PortLabelHidden][Vector2][Integer][Range(-1, 16, -1, -1)] public ValueInput LimitMin;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange(0.5)] public ValueInput DotSize;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput Shape;
    [DoNotSerialize][PortLabelHidden][Scalar][Integer][Range(1, 16, 1)] public ValueInput InnerSteps;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange(0.9)] public ValueInput Hardness;
    [DoNotSerialize][PortLabelHidden][Vector2][OneRange] public ValueInput Center;
    [DoNotSerialize][PortLabelHidden][Scalar][RotationRange] public ValueInput Rotation;
    [DoNotSerialize][Color] public ValueInput InnerColor;
    [DoNotSerialize][Color] public ValueInput OuterColor;
    [DoNotSerialize] public ValueInput ColorIsStep;
    [DoNotSerialize][Color] public ValueInput BackgroundColor;

    protected override void Definition() {
      base.Definition();

      CellSize = ValueInput<DValue>(nameof(CellSize), 0.5);
      LimitMin = ValueInput<DValue>(nameof(LimitMin), Vector2.one * -1.0f);
      LimitMax = ValueInput<DValue>(nameof(LimitMax), Vector2.one * -1.0f);
      DotSize = ValueInput<DValue>(nameof(DotSize), 0.5);
      Shape = ValueInput<DValue>(nameof(Shape), 0.0);
      InnerSteps = ValueInput<DValue>(nameof(InnerSteps), 1);
      Hardness = ValueInput<DValue>(nameof(Hardness), 0.9);
      Center = ValueInput<DValue>(nameof(Center), Vector2.zero);
      Rotation = ValueInput<DValue>(nameof(Rotation), 0.0);
      InnerColor = ValueInput<DValue>(nameof(InnerColor), Color.white);
      OuterColor = ValueInput<DValue>(nameof(OuterColor), Color.gray);
      ColorIsStep = ValueInput<bool>(nameof(ColorIsStep), false);
      BackgroundColor = ValueInput<DValue>(nameof(BackgroundColor), Color.black);
    }

    protected override string ShaderPath => "Hidden/GenDotScreen";

    protected override void SetMaterialProperties(Flow flow, Material material) {
      base.SetMaterialProperties(flow, material);

      float steps = Mathf.Round(flow.GetValue<DValue>(InnerSteps));
      material.SetVector(_DotParams, new Vector4(
          1.0f / flow.GetValue<DValue>(CellSize),
          2.0f / Mathf.Max((float)UnityUtils.DefaultEpsilon, flow.GetValue<DValue>(DotSize)),
          steps,
          Mathf.Log(Mathf.Max(0.0f, flow.GetValue<DValue>(Hardness)) + 1.0f)));
      Vector2 center = flow.GetValue<DValue>(Center) + Vector2.one * 0.5f;
      float rotation = flow.GetValue<DValue>(Rotation);
      material.SetVector(_PositionParams, new Vector4(center.x,
                                                      center.y,
                                                      Mathf.Cos(rotation * -Mathf.Deg2Rad),
                                                      Mathf.Sin(rotation * -Mathf.Deg2Rad)));
      double shapeParam = flow.GetValue<DValue>(Shape);
      material.SetVector(_ShapeParams, new Vector4((float)shapeParam, 0.0f, 0.0f, 0.0f));
      Vector2Int limitMax = NegativeToMax(Vector2Int.RoundToInt(flow.GetValue<DValue>(LimitMax)));
      Vector2Int limitMin = NegativeToMax(Vector2Int.RoundToInt(flow.GetValue<DValue>(LimitMin)));
      material.SetVector(_LimitParams, new Vector4(limitMin.x, limitMin.y, -limitMax.x, -limitMax.y));
      Vector4 innerColor = UnityUtils.ToHslRadians(flow.GetValue<DValue>(InnerColor));
      Vector4 outerColor = UnityUtils.ToHslRadians(flow.GetValue<DValue>(OuterColor));
      Vector4 colorStep = outerColor - innerColor;
      if (!flow.GetValue<bool>(ColorIsStep)) {
        colorStep /= Mathf.Max(1.0f, (steps - 1));
      }
      material.SetColor(_BaseColorHSL, innerColor);
      material.SetColor(_ColorStepHSL, colorStep);
      material.SetColor(_BackgroundColor, flow.GetValue<DValue>(BackgroundColor));
      if (shapeParam < UnityUtils.DefaultEpsilon) {
        material.EnableKeyword(SHAPE_CIRCLE);
        material.DisableKeyword(SHAPE_CIRCLE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE_SQUARE);
        material.DisableKeyword(SHAPE_SQUARE);
      } else if (shapeParam < 0.5 - UnityUtils.DefaultEpsilon) {
        material.EnableKeyword(SHAPE_CIRCLE_SQUIRCLE);
        material.DisableKeyword(SHAPE_CIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE_SQUARE);
        material.DisableKeyword(SHAPE_SQUARE);
      } else if (shapeParam < 0.5 + UnityUtils.DefaultEpsilon) {
        material.EnableKeyword(SHAPE_SQUIRCLE);
        material.DisableKeyword(SHAPE_CIRCLE);
        material.DisableKeyword(SHAPE_CIRCLE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE_SQUARE);
        material.DisableKeyword(SHAPE_SQUARE);
      } else if (shapeParam < 1.0 - UnityUtils.DefaultEpsilon) {
        material.EnableKeyword(SHAPE_SQUIRCLE_SQUARE);
        material.DisableKeyword(SHAPE_CIRCLE);
        material.DisableKeyword(SHAPE_CIRCLE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUARE);
      } else {
        material.EnableKeyword(SHAPE_SQUARE);
        material.DisableKeyword(SHAPE_CIRCLE);
        material.DisableKeyword(SHAPE_CIRCLE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE);
        material.DisableKeyword(SHAPE_SQUIRCLE_SQUARE);
      }
    }

    private static Vector2Int NegativeToMax(Vector2Int value) {
      if (value.x < 0) {
        value.x = int.MaxValue;
      }
      if (value.y < 0) {
        value.y = int.MaxValue;
      }
      return value;
    }
  }
}
