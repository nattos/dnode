using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace NanoGraph.VisualScripting {
  [Inspector(typeof(BaseNode))]
  public class BaseNodeInspector : UnitInspector {
    public const float _previewHeight = 100.0f;
    public Texture2D _captureTexture = Texture2D.redTexture;
    public bool _previewAlphaChannel;

    public BaseNodeInspector(Metadata metadata) : base(metadata) {
      // TODO: HACKY.
      CheckNodeStateListeners();
    }

    protected override float GetHeight(float width, GUIContent label) {
      int editableAttributeCount = ((metadata.value as BaseNode)?.Node as IEditableAttributeProvider)?.EditableAttributes?.Count() ?? 0;
      editableAttributeCount += 2;
      return base.GetHeight(width, label) + EditorGUIUtility.singleLineHeight * editableAttributeCount + _previewHeight;
    }

    protected override void OnGUI(Rect position, GUIContent label) {
      BaseNode node = metadata.value as BaseNode;
      if (node == null) {
        return;
      }

      Rect rect = position;
      rect.height = EditorGUIUtility.singleLineHeight;

      IEditableAttributeProvider attributesProvider = node.Node as IEditableAttributeProvider;
      IReadOnlyList<EditableAttribute> editableAttributes = attributesProvider?.EditableAttributes ?? Array.Empty<EditableAttribute>();
      foreach (var attrib in editableAttributes) {
        object resultValue = null;
        using (var check = new EditorGUI.ChangeCheckScope()) {
          if (attrib.Type == typeof(string)) {
            resultValue = EditorGUI.TextField(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as string ?? "");
          } else if (attrib.Type == typeof(int)) {
            resultValue = EditorGUI.IntField(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as int? ?? 0);
          } else if (attrib.Type == typeof(float)) {
            resultValue = EditorGUI.FloatField(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as float? ?? 0.0f);
          } else if (attrib.Type == typeof(double)) {
            resultValue = (double)EditorGUI.FloatField(rect, attrib.Name, (float)(attrib.Getter.Invoke(attributesProvider) as double? ?? 0.0));
          } else if (attrib.Type == typeof(bool)) {
            resultValue = EditorGUI.Toggle(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as bool? ?? false);
          } else if (attrib.Type.IsEnum) {
            resultValue = EditorGUI.EnumPopup(rect, attrib.Name, attrib.Getter.Invoke(attributesProvider) as Enum);
          }
          if (check.changed) {
            attrib.Setter.Invoke(attributesProvider, resultValue);
            node.PortsChanged();
          }
        }
        rect.y += EditorGUIUtility.singleLineHeight;
      }
      if (GUI.Button(rect, "Dump Debug Info")) {
        EditorApplication.delayCall += () => {
          Debug.Log((node.Node as DataNode)?.DebugInfoDump);
        };
      }
      rect.y += EditorGUIUtility.singleLineHeight;
      if (GUI.Button(rect, "Compile")) {
        EditorApplication.delayCall += () => {
          // foreach (var unit in node.graph.units) {
          //   if (!(unit is BaseNode otherNode)) {
          //     continue;
          //   }
          //   otherNode.SyncInputConnectionsToGraphEdges();
          // }
          IComputeNode computeNode = node.Node as IComputeNode;
          if (computeNode == null) {
            return;
          }
          node.Node.Graph.GenerateProgram(new[] { computeNode });
        };
      }
      rect.y += EditorGUIUtility.singleLineHeight;

      Rect innerRect = position;
      innerRect.height -= _previewHeight;
      Rect lowerRect = position;
      lowerRect.y = innerRect.yMax;
      lowerRect.height = _previewHeight;
      base.OnGUI(innerRect, label);
      OnPreviewGui(lowerRect);
    }

    private void OnPreviewGui(Rect rect) {
      float topPadding = EditorGUIUtility.singleLineHeight;
      float yPadding = 1.0f;
      float yPos = rect.yMin + topPadding;
      Rect descRect = rect;
      descRect.y = yPos;
      descRect.height = EditorGUIUtility.singleLineHeight;
      yPos = descRect.yMax + yPadding;
      Rect previewAlphaRect = rect;
      previewAlphaRect.y = yPos;
      previewAlphaRect.height = EditorGUIUtility.singleLineHeight;
      yPos = previewAlphaRect.yMax + yPadding;
      Rect previewRect = rect;
      previewRect.y = yPos;
      previewRect.height = rect.yMax - yPos;

      EditorGUI.LabelField(descRect, $"Texture: {_captureTexture.width}x{_captureTexture.height}");
      _previewAlphaChannel = EditorGUI.ToggleLeft(previewAlphaRect, "Alpha channel", _previewAlphaChannel);
      if (_previewAlphaChannel) {
        EditorGUI.DrawTextureAlpha(previewRect, _captureTexture, scaleMode: ScaleMode.ScaleToFit);
      } else {
        EditorGUI.DrawPreviewTexture(previewRect, _captureTexture, mat: null, scaleMode: ScaleMode.ScaleToFit);
      }
    }




    private static bool _connectionListenersAdded = false;

    public static void CheckNodeStateListeners() {
      if (!_connectionListenersAdded) {
        _connectionListenersAdded = true;
        DNode.DValueConnectionWidget.ConnectionChanged += connection => {
          try {
            if (connection.destination.unit is SubgraphUnit destSubgraphUnit) {
              BaseNode.MapSubgraphUnit(destSubgraphUnit);
            }
            if (connection.source.unit is SubgraphUnit srcSubgraphUnit) {
              BaseNode.MapSubgraphUnit(srcSubgraphUnit);
            }
          } catch (KeyNotFoundException e) {
            return;
          }
          if (BaseNode.GetSourceBaseNodeOrNull(connection.source.unit, connection.source.key, out _) is BaseNode sourceNode) {
            try {
              sourceNode.NotifyOutputConnectionsChanged();
            } catch {}
          }
          foreach (BaseNode node in BaseNode.GetDestBaseNodes(connection.destination.unit, connection.destination.key)) {
            try {
              node.NotifyInputConnectionsChanged();
            } catch {}
          }
          // (connection.destination.unit as BaseNode)?.NotifyInputConnectionsChanged();
        };
      }
    }
  }
}
