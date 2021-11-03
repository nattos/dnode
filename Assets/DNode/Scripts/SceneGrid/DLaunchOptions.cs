using Unity.VisualScripting;

namespace DNode {
  public enum DGlobalLaunchQuantization {
    None,
    Note32nd,
    Note16nd,
    NoteQuater,
    Bar,
    Bar2,
    Bar4,
    Bar8,
    Bar16,
  }

  public enum DLaunchQuantization {
    Global,
    None,
    Note32nd,
    Note16nd,
    NoteQuater,
    Bar,
    Bar2,
    Bar4,
    Bar8,
    Bar16,
  }

  public enum DLaunchFollowActionType {
    None,
    Stop,
    PlayAgain,
    Previous,
    Next,
    First,
    Last,
    Any,
    Other,
    Jump,
  }

  [IncludeInSettings(true)]
  public struct DLaunchOptions {
    public DLaunchQuantization Quantization;
    public double LengthBeats;
    public DLaunchFollowActionType FollowActionA;
    public DLaunchFollowActionType FollowActionB;
    [InspectorRange(0, 1)]
    public double FollowABChance;

    public static DLaunchOptions Make(DLaunchQuantization quantization) {
      return new DLaunchOptions { Quantization = quantization };
    }

    public static DLaunchOptions MakeChance(DLaunchQuantization quantization, double lengthBeats, DLaunchFollowActionType followActionA, DLaunchFollowActionType followActionB, double followABChance) {
      return new DLaunchOptions {
        Quantization = quantization,
        LengthBeats = lengthBeats,
        FollowActionA = followActionA,
        FollowActionB = followActionB,
        FollowABChance = followABChance,
      };
    }
  }
}
