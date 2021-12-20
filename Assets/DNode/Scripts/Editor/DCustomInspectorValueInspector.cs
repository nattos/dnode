using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace DNode {
  [Inspector(typeof(DCustomInspectorValue))]
  public class DCustomInspectorValueInspector : Inspector {
    private readonly AttributeCache _attributeCache = new AttributeCache();

    public DCustomInspectorValueInspector(Metadata metadata) : base(metadata) {}

    protected override void OnGUI(Rect position, GUIContent label) {
      // position = BeginLabeledBlock(metadata, position, label);
      EditorGUI.BeginChangeCheck();
      var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

      DCustomInspectorValue oldValue = (DCustomInspectorValue)metadata.value;
      var inspectorDataProvider = metadata.parent.parent.parent.value as IDCustomInspectorDataProvider;
      var inspectorData = inspectorDataProvider?.ProvideCustomInspectorData(oldValue.Key);
      DValueInspector.DValueField(rect, metadata, oldValue.Value, _attributeCache, out DValue outValue, inspectorData);

      // if (EndBlock(metadata)) {
      if (EditorGUI.EndChangeCheck()) {
        metadata.RecordUndo();
        metadata.value = new DCustomInspectorValue { Value = outValue, Key = oldValue.Key };
      }
    }

    protected override void OnEditorPrefGUI(Rect position, GUIContent label) {}

    protected override float GetHeight(float width, GUIContent label) {
      return DValueInspector.GetDValidFieldSize(metadata, _attributeCache).height;
    }

    public override float GetAdaptiveWidth() {
      return DValueInspector.GetDValidFieldSize(metadata, _attributeCache).width;
    }
  }
}
