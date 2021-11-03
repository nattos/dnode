namespace DNode {
  public class DTexDither : DTexUnaryPreserveAlphaBlitUnit {
    protected override void Definition() {
      base.Definition();
    }
    protected override string ShaderPath => "Hidden/TexDither";
  }
}
