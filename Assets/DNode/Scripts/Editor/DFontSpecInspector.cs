// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;
// using Unity.VisualScripting;
// using UnityEditor;
// using UnityEngine;

// namespace DNode {
//   [Inspector(typeof(DFontSpec))]
//   public class DFontSpecInspector : Inspector {
//     private const string _defaultFontTag = "<Default>";

//     private static readonly Func<string[]> _allFontNames = UnityEditorUtils.RateLimitedFunc(5.0f, () => {
//       MeshGlyphCache.LoadFontNameCache();
//       return new[] { _defaultFontTag }.Concat(MeshGlyphCache.AllFontNamesImmediate).ToArray();
//     });

//     public DFontSpecInspector(Metadata metadata) : base(metadata) {}

//     protected override void OnGUI(Rect position, GUIContent label) {
//       position = BeginLabeledBlock(metadata, position, label);
//       var fieldPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
//       DFontSpec oldValue = (DFontSpec)metadata.value;
//       var rangeAttribute = metadata.GetAttribute<InspectorRangeAttribute>();

//       string[] allFontNames = _allFontNames.Invoke();
//       int selectedIndex = Math.Max(0, Array.IndexOf(allFontNames, oldValue.FontName));
//       int newSelectedIndex = EditorGUI.Popup(fieldPosition, selectedIndex, allFontNames);

//       if (EndBlock(metadata)) {
//         metadata.RecordUndo();
//         string fontName = newSelectedIndex <= 0 || newSelectedIndex >= allFontNames.Length ? null : allFontNames[newSelectedIndex];
//         metadata.value = new DFontSpec { FontName = fontName };
//       }
//     }

//     protected override void OnEditorPrefGUI(Rect position, GUIContent label) {
//       position = BeginLabeledBlock(metadata, position, label);
//       var fieldPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight - 2);
//       DFontSpec oldValue = (DFontSpec)metadata.value;
//       var rangeAttribute = metadata.GetAttribute<InspectorRangeAttribute>();

//       string[] allFontNames = _allFontNames.Invoke();
//       int selectedIndex = Math.Max(0, Array.IndexOf(allFontNames, oldValue.FontName));
//       int newSelectedIndex = EditorGUI.Popup(fieldPosition, selectedIndex, allFontNames);

//       if (EndBlock(metadata)) {
//         metadata.RecordUndo();
//         string fontName = newSelectedIndex <= 0 || newSelectedIndex >= allFontNames.Length ? null : allFontNames[newSelectedIndex];
//         metadata.value = new DFontSpec { FontName = fontName };
//       }
//     }

//     protected override float GetHeight(float width, GUIContent label) {
//       return HeightWithLabel(metadata, width, EditorGUIUtility.singleLineHeight, label);
//     }

//     public override float GetAdaptiveWidth() {
//       return 100;
//     }
//   }
// }
