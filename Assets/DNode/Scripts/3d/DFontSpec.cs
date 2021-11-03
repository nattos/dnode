namespace DNode {
  public struct DFontSpec {
    public string FontName;

    public static implicit operator DFontSpec(string value) {
      return new DFontSpec { FontName = value };
    }

    public static implicit operator string(DFontSpec value) {
      return value.FontName;
    }

    static DFontSpec() {
      DValue.Init();
    }
  }
}
