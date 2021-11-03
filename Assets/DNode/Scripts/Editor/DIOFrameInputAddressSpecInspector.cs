using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace DNode {
  [Inspector(typeof(DIOFrameInputAddressSpec))]
  public class DIOFrameInputAddressSpecInspector : Inspector {
    private const int _buttonWidth = 80;

    public DIOFrameInputAddressSpecInspector(Metadata metadata) : base(metadata) {}

    protected override void OnGUI(Rect position, GUIContent label) {
      position = BeginLabeledBlock(metadata, position, label);
      var fieldPosition = new Rect(position.x, position.y, position.width - _buttonWidth, EditorGUIUtility.singleLineHeight);
      var buttonPosition = new Rect(position.x + position.width - _buttonWidth, position.y, _buttonWidth, EditorGUIUtility.singleLineHeight);
      DIOFrameInputAddressSpec oldValue = (DIOFrameInputAddressSpec)metadata.value;
      var rangeAttribute = metadata.GetAttribute<InspectorRangeAttribute>();

      bool stringAddressChanged = false;
      string newStringAddress;
      using (var check = new EditorGUI.ChangeCheckScope()) {
        newStringAddress = EditorGUI.TextField(fieldPosition, oldValue.Address);
        stringAddressChanged = check.changed;
      }

      string[] allAddresses = Klak.Spout.SpoutManager.GetSourceNames();
      int selectedIndex = Array.IndexOf(allAddresses, oldValue.Address);
      bool selectedIndexChanged = false;
      int newSelectedIndex;
      using (var check = new EditorGUI.ChangeCheckScope()) {
        newSelectedIndex = EditorGUI.Popup(buttonPosition, selectedIndex, allAddresses);
        selectedIndexChanged = check.changed;
      }

      if (EndBlock(metadata)) {
        metadata.RecordUndo();
        string newAddress;
        if (stringAddressChanged) {
          newAddress = newStringAddress;
        } else {
          newAddress = newSelectedIndex < 0 || newSelectedIndex >= allAddresses.Length ? null : allAddresses[newSelectedIndex];
        }
        metadata.value = new DIOFrameInputAddressSpec { Address = newAddress };
      }
    }

    protected override void OnEditorPrefGUI(Rect position, GUIContent label) {
      position = BeginLabeledBlock(metadata, position, label);
      if (EndBlock(metadata)) {
      }
    }

    protected override float GetHeight(float width, GUIContent label) {
      return HeightWithLabel(metadata, width, EditorGUIUtility.singleLineHeight, label);
    }

    public override float GetAdaptiveWidth() {
      return 250;
    }
  }
}
