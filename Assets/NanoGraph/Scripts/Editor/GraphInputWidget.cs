using Unity.VisualScripting;

namespace NanoGraph.VisualScripting {
  [Widget(typeof(GraphInput))]
  public sealed class GraphInputWidget : UnitWidget<GraphInput> {
    public GraphInputWidget(FlowCanvas canvas, GraphInput unit) : base(canvas, unit) { }

    protected override NodeColorMix baseColor => NodeColorMix.TealReadable;
  }
}
