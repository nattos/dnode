using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace DNode {
  [Inspector(typeof(DEvent))]
  public class DEventInspector : Inspector {
    private const float _triggerButtonWidth = 32;

    private readonly AttributeCache _attributeCache = new AttributeCache();

    public DEventInspector(Metadata metadata) : base(metadata) {}

    protected override void OnGUI(Rect position, GUIContent label) {
      // position = BeginLabeledBlock(metadata, position, label);
      EditorGUI.BeginChangeCheck();

      var fieldRect = new Rect(position.x, position.y, position.width - _triggerButtonWidth, EditorGUIUtility.singleLineHeight);
      var triggerRect = new Rect(position.x + position.width - _triggerButtonWidth, position.y, _triggerButtonWidth, EditorGUIUtility.singleLineHeight);
      
      DEvent oldValue = (DEvent)metadata.value;
      DValueInspector.DValueField(fieldRect, metadata, oldValue.ImmediateValue, _attributeCache, out DValue newValue);

      bool oldTriggered = oldValue.IsTriggered;
      bool newTriggered = oldTriggered;
      if (UnityEditorUtils.IsFieldEditable(metadata)) {
        newTriggered = EditorGUI.Toggle(triggerRect, oldTriggered);
      }
      // if (EndBlock(metadata)) {
      if (EditorGUI.EndChangeCheck()) {
        metadata.RecordUndo();
        metadata.value = DEvent.CreateImmediate(newValue, triggered: newTriggered);
      }
    }

    protected override void OnEditorPrefGUI(Rect position, GUIContent label) {}

    protected override float GetHeight(float width, GUIContent label) {
      return DValueInspector.GetDValidFieldSize(metadata, _attributeCache).height;
    }

    public override float GetAdaptiveWidth() {
      return DValueInspector.GetDValidFieldSize(metadata, _attributeCache).width + _triggerButtonWidth;
    }
  }
}
