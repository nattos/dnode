﻿using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DMotionBlurSprite : DFrameUnit {
    [DoNotSerialize] public ValueInput Texture;
    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Vector3][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;
    [DoNotSerialize][PortLabelHidden][Vector3][ScaleRange] public ValueInput Scale;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange] public ValueInput MotionVector;
    [DoNotSerialize][PortLabelHidden][Scalar][ZeroOneRange] public ValueInput MotionVectorMix;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      Texture = ValueInput<Texture>(nameof(Texture), null).AllowsNull();
      Position = ValueInput<DValue>(nameof(Position), 0.0);
      Rotation = ValueInput<DValue>(nameof(Rotation), 0.0);
      Scale = ValueInput<DValue>(nameof(Scale), Vector3.one);
      MotionVector = ValueInput<DValue>(nameof(MotionVector), 0.0);
      MotionVectorMix = ValueInput<DValue>(nameof(MotionVectorMix), 0.5);
 
      DFrameArray<DFrameObject> ComputeFromFlow(Flow flow) {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DNode/Prefabs/DMotionBlurSprite.prefab");
        Texture texture = flow.GetValue<Texture>(Texture).OrNull() ?? Texture2D.whiteTexture;
        DValue motionVector = flow.GetValue<DValue>(MotionVector);
        DValue motionVectorMix = flow.GetValue<DValue>(MotionVectorMix);

        var materialPropertyBlock = new MaterialPropertyBlock();
        materialPropertyBlock.SetTexture("_Texture2D", texture);

        DValue position = flow.GetValue<DValue>(Position);
        DValue rotation = flow.GetValue<DValue>(Rotation);
        DValue scale = flow.GetValue<DValue>(Scale);
        int rows = Math.Max(position.Rows, Math.Max(rotation.Rows, scale.Rows));

        DMutableFrameArray<DFrameObject> result = new DMutableFrameArray<DFrameObject>(rows);
        for (int row = 0; row < rows; ++row) {
          (GameObject instance, _) = PrefabCache.InstantiatePrefab(this, null, prefab);
          TransformComponent transform = TransformComponent.GetOrAdd(instance);
          if (transform) {
            transform.LocalPosition.Value = position.Vector3FromRow(row);
            transform.LocalRotation.Value = Quaternion.Euler(rotation.Vector3FromRow(row));
            transform.LocalScale.Value = scale.Vector3FromRow(row, Vector3.one);
          }

          materialPropertyBlock.SetVector("_ExtraMotion", new Vector3((float)motionVector[row, 0], (float)motionVector[row, 1], (float)motionVectorMix[row, 0]));
          instance.GetComponent<Renderer>().SetPropertyBlock(materialPropertyBlock);

          result[row] = new DFrameObject { GameObject = instance };
        }
        return result.ToValue();
      }

      result = ValueOutput<DFrameArray<DFrameObject>>("result", DNodeUtils.CachePerFrame(ComputeFromFlow));
    }
  }
}
