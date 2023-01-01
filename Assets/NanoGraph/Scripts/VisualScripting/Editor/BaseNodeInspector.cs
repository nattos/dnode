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
    public const float _messagesHeight = 300.0f;
    private float _attributesHeight = 0.0f;

    private static Lazy<Styles> _styles = new Lazy<Styles>(() => new Styles());

    public BaseNodeInspector(Metadata metadata) : base(metadata) {
      // TODO: HACKY.
      CheckNodeStateListeners();
    }

    protected override float GetHeight(float width, GUIContent label) {
      _attributesHeight = EditableAttributesInspector.GetHeight(width, (metadata.value as BaseNode)?.Node as IEditableAttributeProvider);
      return base.GetHeight(width, label) + _attributesHeight + _messagesHeight + EditorGUIUtility.singleLineHeight * 2;
    }

    protected override void OnGUI(Rect position, GUIContent label) {
      BaseNode node = metadata.value as BaseNode;
      if (node == null) {
        return;
      }

      Rect rect = position;
      using (var check = new EditorGUI.ChangeCheckScope()) {
        rect.height = _attributesHeight;
        EditableAttributesInspector.OnGUI(rect, node.Node as IEditableAttributeProvider);
        rect.y += rect.height;

        if (check.changed) {
          node.PortsChanged();
        }
      }

      rect.height = EditorGUIUtility.singleLineHeight;
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
      innerRect.height -= _messagesHeight;
      Rect lowerRect = position;
      lowerRect.y = innerRect.yMax;
      lowerRect.height = _messagesHeight;
      base.OnGUI(innerRect, label);
      DrawMessages(lowerRect);
    }

    private void DrawMessages(Rect rect) {
      IReadOnlyList<string> messages = (metadata.value as BaseNode)?.Node?.Messages ?? Array.Empty<string>();
      EditorGUI.TextArea(rect, string.Join("\n\n", messages) + "\n", _styles.Value.WrappedTextArea);
    }


    private new class Styles {
      public Styles() {
        WrappedTextArea = new GUIStyle(EditorStyles.textArea);
        WrappedTextArea.wordWrap = true;
      }

      public readonly GUIStyle WrappedTextArea;
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
