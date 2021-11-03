using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DText : DFrameUnit {
    [DoNotSerialize] public ValueInput Text;
    [DoNotSerialize] public ValueInput Font;
    [DoNotSerialize][PortLabelHidden][Vector3][WorldRange] public ValueInput Position;
    [DoNotSerialize][PortLabelHidden][Vector3][RotationRange][ClampMode(ClampMode.Wrap)] public ValueInput Rotation;
    [DoNotSerialize][PortLabelHidden][Vector3][ScaleRange] public ValueInput Scale;
    [DoNotSerialize][PortLabelHidden][Vector2][ZeroOneRange(defaultValue: 0.5)] public ValueInput Anchor;

    [Inspectable] public bool Monospace = false;
    [Inspectable] public bool AnchorToBaseline = true;
    
    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    [DoNotSerialize] public ValueOutput Width;
    [DoNotSerialize] public ValueOutput Height;

    private readonly MeshGlyphLayout _layout = new MeshGlyphLayout();

    protected override void Definition() {
      Text = ValueInput<string>(nameof(Text), "");
      Font = ValueInput<DFontSpec>(nameof(Font), default);
      Position = ValueInput<DValue>(nameof(Position), 0.0);
      Rotation = ValueInput<DValue>(nameof(Rotation), 0.0);
      Scale = ValueInput<DValue>(nameof(Scale), 1.0);
      Anchor = ValueInput<DValue>(nameof(Anchor), new Vector2(0.5f, 0.5f));
 
      (Vector2 size, DFrameArray<DFrameObject> objects) ComputeFromFlow(Flow flow) {
        MeshGlyphLayout layout = _layout;
        layout.Text = flow.GetValue<string>(Text);
        MeshGlyphFont font = null;
        string fontName = flow.GetValue<DFontSpec>(Font).FontName;
        if (!string.IsNullOrEmpty(fontName)) {
          font = MeshGlyphCache.GetFontByName(fontName);
        }
        layout.Font = font ?? DScriptMachine.CurrentInstance.DefaultFont;
        layout.ForceMonospace = Monospace;

        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DNode/Prefabs/Glyph.prefab");

        DValue position = flow.GetValue<DValue>(Position);
        DValue rotation = flow.GetValue<DValue>(Rotation);
        DValue scale = flow.GetValue<DValue>(Scale);
        DValue anchor = flow.GetValue<DValue>(Anchor);
        float anchorX = (float)anchor[0, 0];
        float anchorY = (float)anchor[0, 1];
        bool anchorToBaseline = AnchorToBaseline;

        IReadOnlyList<MeshGlyphLayout.Glyph> glyphs = layout.LayoutGlyphs;
        float layoutWidth = layout.LayoutWidth;

        int rows = glyphs.Count;

        Vector3 anchorPosition = position.Vector3FromRow(0);
        Quaternion anchorRotation = Quaternion.Euler(rotation.Vector3FromRow(0));
        Vector3 localScale = scale.Vector3FromRow(0);

        float xBias = UnityUtils.Lerp(0.0f, -layoutWidth, anchorX);
        float yMin = anchorToBaseline ? 0.0f : layout.LayoutDescent;
        float yMax = layout.LayoutCapHeight;
        float yBias = -UnityUtils.Lerp(yMin, yMax, anchorY);
        DMutableFrameArray<DFrameObject> result = new DMutableFrameArray<DFrameObject>(rows);
        for (int row = 0; row < rows; ++row) {
          var glyph = glyphs[row];

          (GameObject instance, bool isNew) = PrefabCache.InstantiatePrefab(this, null, prefab);
          if (isNew) {
            instance.name = $"Glyph {char.ConvertFromUtf32(glyph.Codepoint)}";
          }
          TransformComponent transform = TransformComponent.GetOrAdd(instance);
          if (transform) {
            Vector3 rawPosition = new Vector3(glyph.PositionMin + xBias, yBias, 0.0f);
            Vector3 transformedPosition = anchorRotation * rawPosition.ElementMul(localScale);
            transform.LocalPosition.Value = anchorPosition + transformedPosition;
            transform.LocalRotation.Value = anchorRotation;
            transform.LocalScale.Value = localScale;
          }

          var meshGlyph = instance.GetComponent<MeshGlyph>();
          meshGlyph.MeshOverride = glyph.Mesh;

          result[row] = new DFrameObject { GameObject = instance };
        }

        float sizeX = layoutWidth * localScale.x;
        float sizeY = (yMax - yMin) * localScale.y;
        return (new Vector2(sizeX, sizeY), result.ToValue());
      }
      var resultFunc = DNodeUtils.CachePerFrame(ComputeFromFlow);
      result = ValueOutput<DFrameArray<DFrameObject>>("result", flow => resultFunc.Invoke(flow).objects);
      Width = ValueOutput<float>("Width", flow => resultFunc.Invoke(flow).size.x);
      Height = ValueOutput<float>("Height", flow => resultFunc.Invoke(flow).size.y);
    }
  }
}
