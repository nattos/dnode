using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class DRootNode : Unit {
    [DoNotSerialize]
    public ControlInput inputTrigger;

    [DoNotSerialize]
    public ValueInput FrameOutput;

    private readonly List<DIOOutputNode> _executeAfterNodesScratch = new List<DIOOutputNode>();

    protected override void Definition() {
      FrameOutput = ValueInput<DFrameNodes>("FrameOutput");

      inputTrigger = ControlInput("inputTrigger", (flow) => {
        // Pull environment.
        DEnvironmentOverrides env = new DEnvironmentOverrides {
          OutputSize = new Vector2Int(DScriptMachine.CurrentInstance.RenderTextureCache.ScreenWidth, DScriptMachine.CurrentInstance.RenderTextureCache.ScreenHeight),
        };
        foreach (var node in DEnvironmentOverrideProviderHandle.GetProvidersForGraph(graph) ?? Array.Empty<DEnvironmentOverrideProviderHandle>()) {
          env = DEnvironmentOverrides.Merge(env, node.OverridesProvider(flow));
        }
        DScriptMachine.CurrentInstance.Environment = env;

        // Update transport;
        var transport = DScriptMachine.CurrentInstance.Transport;
        if (env.LoopLengthBars != null) {
          transport.LoopLengthBars = env.LoopLengthBars.Value;
        }
        if (env.BeatsPerBar != null) {
          transport.BeatsPerBar = env.BeatsPerBar.Value;
        }
        if (env.Tempo != null) {
          transport.Tempo = env.Tempo.Value;
        }
        if (env.TimeBeats != null) {
          transport.DriveFromTimeBeats(env.TimeBeats.Value);
        } else {
          transport.DriveFromTimeSeconds(UnityEngine.Time.timeAsDouble);
        }

        // Pull output nodes.
        _executeAfterNodesScratch.Clear();
        var outputNodes = DIOOutputNode.GetNodesForGraph(graph) ?? Array.Empty<DIOOutputNode>();
        DScriptMachine.CurrentInstance.ReportedOutputNodeCount = outputNodes.Count;
        foreach (var node in outputNodes) {
          if (node.ExecuteAfterGraph) {
            _executeAfterNodesScratch.Add(node);
          } else {
            node.ComputeFromFlow(flow);
          }
        }

        DFrameNodes frameData = FrameOutput.connectedPorts.Any() ? flow.GetValue<DFrameNodes>(FrameOutput) : default;
        flow.stack.gameObject.GetComponent<DScriptMachine>().ExportFrameData(new DFrameData { Nodes = frameData });

        foreach (var node in _executeAfterNodesScratch) {
          node.ComputeFromFlow(flow);
        }
        _executeAfterNodesScratch.Clear();

        return null;
      });
    }
  }
}
