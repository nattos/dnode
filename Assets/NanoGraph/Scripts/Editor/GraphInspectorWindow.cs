using NanoGraph.Plugin;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;

namespace NanoGraph.VisualScripting {
  public class GraphInspectorWindow : EditorWindow {
    private IGraphContext _currentGraphContext;
    private GraphInspectorPanel _graphInspectorPanel;
    private Vector2 _scrollPosition;

    public void OnEnable() {
      Instance = this;
    }

    public void OnDisable() {
      Instance = null;
    }

    public void OnGUI() {
      var graphWindow = GraphWindow.active;
      var context = graphWindow?.context;
      if (context != _currentGraphContext) {
        _currentGraphContext = context;
        _graphInspectorPanel = new GraphInspectorPanel(context);
      }
      if (_graphInspectorPanel == null) {
        return;
      }
      Rect clientRect = position;
      using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true))) {
        clientRect.position = Vector2.zero;
        float height = _graphInspectorPanel.GetHeight(clientRect.width);
        clientRect.height = height;
        Rect contentRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.Height(clientRect.height));
        _graphInspectorPanel.OnGUI(contentRect);
        _scrollPosition = scrollView.scrollPosition;
      }
    }

    public static GraphInspectorWindow Instance = null;


    [MenuItem("Do/Graph Inspector Window")]
    public static void ShowWindow() {
      EditorWindow.GetWindow<GraphInspectorWindow>("Edit").Show();
    }
  }
}
