// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using System.Threading.Tasks;
// using Unity.VisualScripting;
// using UnityEditor;
// using UnityEngine;

// namespace DNode {
//   [Inspector(typeof(DLaunchableTriggerValue))]
//   public class DLauchableTriggerInspector : Inspector {
//     private const float _triggerButtonWidth = 32;

//     public DLauchableTriggerInspector(Metadata metadata) : base(metadata) {}

//     protected override void OnGUI(Rect position, GUIContent label) {
//       BeginBlock(metadata, position);
//       try {
//         if (!(this.metadata.value is DLaunchableTriggerValue customData)) {
//           return;
//         }

//         var target = customData?.Target;
//         var buttonPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

//         Color launchColor = target?.StatusPlaying == true ? Color.green : target?.StatusQueued == true ? Color.yellow : target?.HasInput == true ? Color.gray : Color.red;
//         double quantizationPercent = target?.StatusPlaying == true ? target.StatusPlayingQuantizationPercent : target?.StatusQueued == true ? target.StatusQueuedQuantizationPercent : 0.0;
//         launchColor = Color.Lerp(Color.black, launchColor, 0.5f);
//         if (DValueInspector.SemiFilledButton(buttonPosition, target?.LaunchLabelOverride ?? "Launch", launchColor, UnityUtils.LerpAlpha(Color.black, 0.0f, 0.5f), quantizationPercent)) {
//           if (target != null) {
//             target.Triggered = true;
//           }
//         }

//       } finally {
//         EndBlock(metadata);
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
