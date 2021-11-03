namespace DNode {
  public class DTexInvert : DTexUnaryPreserveAlphaBlitUnit {
    protected override void Definition() {
      base.Definition();
    }
    protected override string ShaderPath => "Hidden/TexInvert";
  }
}
