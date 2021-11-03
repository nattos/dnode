using System;
using System.Collections.Generic;

namespace DNode {
  public class DEnvironmentOverrideProviderHandle : IDisposable {
    private static readonly Dictionary<Unity.VisualScripting.FlowGraph, List<DEnvironmentOverrideProviderHandle>> _providers =
        new Dictionary<Unity.VisualScripting.FlowGraph, List<DEnvironmentOverrideProviderHandle>>();

    public static IReadOnlyList<DEnvironmentOverrideProviderHandle> GetProvidersForGraph(Unity.VisualScripting.FlowGraph graph) {
      _providers.TryGetValue(graph, out var providers);
      return providers;
    }

    private Unity.VisualScripting.FlowGraph _graph;
    public readonly Func<Unity.VisualScripting.Flow, DEnvironmentOverrides> OverridesProvider;

    public DEnvironmentOverrideProviderHandle(Unity.VisualScripting.FlowGraph graph,
                                              Func<Unity.VisualScripting.Flow, DEnvironmentOverrides> overridesProvider) {
      _graph = graph;
      OverridesProvider = overridesProvider;

      if (!_providers.TryGetValue(_graph, out var providers)) {
        providers = new List<DEnvironmentOverrideProviderHandle>();
        _providers[_graph] = providers;
      }
      providers.Add(this);
    }

    public void Dispose() {
      if (_providers.TryGetValue(_graph, out var providers)) {
        providers.Remove(this);
      }
    }
  }
}
