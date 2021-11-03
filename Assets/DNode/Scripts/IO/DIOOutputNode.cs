using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public abstract class DIOOutputNode : Unit, IDOutputNode {
    private static readonly Dictionary<FlowGraph, List<DIOOutputNode>> _outputNodes = new Dictionary<FlowGraph, List<DIOOutputNode>>();

    public static IReadOnlyList<DIOOutputNode> GetNodesForGraph(FlowGraph graph) {
      _outputNodes.TryGetValue(graph, out var nodes);
      return nodes;
    }

    private static void RegisterNode(DIOOutputNode node, FlowGraph graph) {
      if (!_outputNodes.TryGetValue(graph, out var nodes)) {
        nodes = new List<DIOOutputNode>();
        _outputNodes[graph] = nodes;
      }
      nodes.Add(node);
    }

    private static void UnregisterNode(DIOOutputNode node, FlowGraph graph) {
      if (_outputNodes.TryGetValue(graph, out var nodes)) {
        nodes.Remove(node);
        if (nodes.Count < 0) {
          _outputNodes.Remove(graph);
        }
      }
    }

    public override void AfterAdd() {
      base.AfterAdd();
      RegisterNode(this, this.graph);
    }

    public override void BeforeRemove() {
      base.BeforeRemove();
      UnregisterNode(this, this.graph);
    }

    public override void Dispose() {
      base.Dispose();
      UnregisterNode(this, this.graph);
    }

    public abstract void ComputeFromFlow(Flow flow);
  }
}
