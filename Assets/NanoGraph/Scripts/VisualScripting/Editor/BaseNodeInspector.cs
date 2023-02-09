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
      BaseNode node = metadata.value as NodeBasedNode;
      float embeddedNodeHeight = 0.0f;
      if (node is EmbeddedNode embeddedNode) {
        embeddedNodeHeight = EditorGUIUtility.singleLineHeight * 2;
      }
      var editableAttributeProvider = node?.EditableAttributeProvider;
      if (editableAttributeProvider != null) {
        _attributesHeight = EditableAttributesInspector.GetHeight(width, (metadata.value as NodeBasedNode)?.Node as IEditableAttributeProvider);
      } else {
        _attributesHeight = 0.0f;
      }
      return base.GetHeight(width, label) + embeddedNodeHeight + _attributesHeight + _messagesHeight + EditorGUIUtility.singleLineHeight * 2;
    }

    protected override void OnGUI(Rect position, GUIContent label) {
      BaseNode node = metadata.value as BaseNode;
      if (node == null) {
        return;
      }

      Rect rect = position;
      if (node is EmbeddedNode embeddedNode) {
        using (var check = new EditorGUI.ChangeCheckScope()) {
          rect.height = EditorGUIUtility.singleLineHeight;
          ScriptGraphAsset newGraph = EditorGUI.ObjectField(rect, embeddedNode.ScriptGraphAssetReference, typeof(ScriptGraphAsset), allowSceneObjects: false) as ScriptGraphAsset;
          rect.y += rect.height;
          if (check.changed) {
            embeddedNode.ScriptGraphAssetReference = newGraph;
            embeddedNode.LoadFromScriptGraphAsset();
          }
        }
        if (GUI.Button(rect, "Refresh Graph")) {
          EditorApplication.delayCall += () => {
            embeddedNode.LoadFromScriptGraphAsset();
          };
        }
        rect.y += rect.height;
        rect.y += rect.height;
      }

      var editableAttributeProvider = node.EditableAttributeProvider;
      if (editableAttributeProvider != null) {
        using (var check = new EditorGUI.ChangeCheckScope()) {
          rect.height = _attributesHeight;
          EditableAttributesInspector.OnGUI(rect, editableAttributeProvider);
          rect.y += rect.height;

          if (check.changed) {
            node.PortsChanged();
          }
        }
      }

      rect.height = EditorGUIUtility.singleLineHeight;
      if (GUI.Button(rect, "Dump Debug Info")) {
        EditorApplication.delayCall += () => {
          node.Define();
          Debug.Log(((node as NodeBasedNode)?.Node as DataNode)?.DebugInfoDump);
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
          IComputeNode computeNode = (node as NodeBasedNode)?.Node as IComputeNode;
          if (computeNode == null) {
            return;
          }
          (node as NodeBasedNode).Node.Graph.GenerateProgram(new[] { computeNode });
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
      IReadOnlyList<string> messages = (metadata.value as NodeBasedNode)?.Node?.Messages ?? Array.Empty<string>();
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
          // if (BaseNode.GetSourceBaseNodeOrNull(connection.source.unit, connection.source.key, out _) is BaseNode sourceNode) {
          if (connection.source.unit is BaseNode sourceNode) {
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
