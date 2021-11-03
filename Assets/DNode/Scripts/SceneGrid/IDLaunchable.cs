namespace DNode {
  public interface IDLaunchable {
    bool StatusPlaying { get; set; }
    bool StatusQueued { get; set; }
    bool HasInput { get; }
    bool Triggered { get; set; }
    DLaunchQuantization LaunchOptionQuantization { get; }
    string LaunchLabelOverride { get; }
  }
}
