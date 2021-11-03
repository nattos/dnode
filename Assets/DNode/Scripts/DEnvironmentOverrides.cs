using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public struct DEnvironmentOverrides {
    public Vector2Int? OutputSize;
    public Vector2Int? InputSize;

    public double? TimeBeats;
    public double? Tempo;
    public double? BeatsPerBar;
    public double? LoopLengthBars;

    public Vector2Int EffectiveOutputSize => OutputSize ?? Vector2Int.zero;
    public Vector2Int EffectiveInputSize => InputSize ?? OutputSize ?? Vector2Int.zero;

    public static DEnvironmentOverrides Merge(DEnvironmentOverrides lhs, DEnvironmentOverrides rhs) {
      return new DEnvironmentOverrides {
        OutputSize = rhs.OutputSize ?? lhs.OutputSize,
        InputSize = rhs.InputSize ?? lhs.InputSize,
        TimeBeats = rhs.TimeBeats ?? lhs.TimeBeats,
        Tempo = rhs.Tempo ?? lhs.Tempo,
        BeatsPerBar = rhs.BeatsPerBar ?? lhs.BeatsPerBar,
        LoopLengthBars = rhs.LoopLengthBars ?? lhs.LoopLengthBars,
      };
    }
  }
}
