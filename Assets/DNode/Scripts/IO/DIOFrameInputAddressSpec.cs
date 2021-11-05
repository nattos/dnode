using Unity.VisualScripting;

namespace DNode {
  [TypeIcon(typeof(string))]
  public struct DIOFrameInputAddressSpec {
    public string Address;

    public static implicit operator DIOFrameInputAddressSpec(string value) {
      return new DIOFrameInputAddressSpec { Address = value };
    }

    public static implicit operator string(DIOFrameInputAddressSpec value) {
      return value.Address;
    }

    static DIOFrameInputAddressSpec() {
      DValue.Init();
    }
  }
}
