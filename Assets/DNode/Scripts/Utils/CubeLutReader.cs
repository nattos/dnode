using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public class CubeLutReader {
    public static Color32[] Read(Stream stream, int desiredSize) {
      string[] lines;
      using (var reader = new StreamReader(stream, Encoding.UTF8)) {
        lines = reader.ReadToEnd().Split('\n');
      }

      int? size1D = null;
      int? size3D = null;
      Vector3 domainMin = Vector3.zero;
      Vector3 domainMax = Vector3.one;
      List<float> values1D = new List<float>();
      List<Vector3> values3D = new List<Vector3>();

      for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber) {
        string line = lines[lineNumber];
        string trimmedLine = line.Trim();
        if (trimmedLine.Length <= 0 || trimmedLine.StartsWith("#")) {
          continue;
        }

        if (TryGetTaggedContent(line, "TITLE", out var _)) {
        } else if (TryGetTaggedContent(line, "DOMAIN_MIN", out string domainMinStr)) {
          domainMin = ParseVector3(domainMinStr) ?? domainMin;
        } else if (TryGetTaggedContent(line, "DOMAIN_MAX", out string domainMaxStr)) {
          domainMax = ParseVector3(domainMaxStr) ?? domainMax;
        } else if (TryGetTaggedContent(line, "LUT_1D_SIZE", out string size1DStr)) {
          size1D = ParseInt(size1DStr) ?? size1D;
        } else if (TryGetTaggedContent(line, "LUT_3D_SIZE", out string size3DStr)) {
          size3D = ParseInt(size3DStr) ?? size3D;
        } else {
          if (size1D != null) {
            values1D.Add(ParseFloat(trimmedLine) ?? default);
          } else if (size3D != null) {
            values3D.Add(ParseVector3(trimmedLine) ?? default);
          }
        }
      }

      Func<Vector3, Vector3> transform;
      if (size1D != null) {
        int size = size1D.Value;
        transform = input =>
            new Vector3(SampleFloat(values1D, input.x, size, 0),
                        SampleFloat(values1D, input.y, size, size),
                        SampleFloat(values1D, input.z, size, size * 2));
      } else if (size3D != null) {
        int size = size3D.Value;
        transform = input => SampleVector3(values3D, input, size);
      } else {
        transform = input => input;
      }

      Color32[] colors = new Color32[desiredSize * desiredSize * desiredSize];
      for (int z = 0; z < desiredSize; ++z) {
        for (int y = 0; y < desiredSize; ++y) {
          for (int x = 0; x < desiredSize; ++x) {
            Vector3 inputColor = new Vector3(x / (float)(desiredSize - 1), y / (float)(desiredSize - 1), z / (float)(desiredSize - 1));
            Vector3 inputDomainColor = new Vector3(UnityUtils.Lerp(domainMin.x, domainMax.x, inputColor.x),
                                                   UnityUtils.Lerp(domainMin.y, domainMax.y, inputColor.y),
                                                   UnityUtils.Lerp(domainMin.z, domainMax.z, inputColor.z));
            colors[x + y * desiredSize + z * desiredSize * desiredSize] = ToColor32(transform.Invoke(inputDomainColor));
          }
        }
      }
      return colors;
    }

    private const float _logCCut = 0.011361f;
    private const float _logCA = 5.555556f;
    private const float _logCB = 0.047996f;
    private const float _logCC = 0.244161f;
    private const float _logCD = 0.386036f;
    private const float _logCE = 5.301883f;
    private const float _logCF = 0.092819f;
  
    private static float LinearToLogCScalar(float x) {
      float o;
      if (x > _logCCut) {
        o = _logCC * Mathf.Log10(Mathf.Max(_logCA * x + _logCB, 0.0f)) + _logCD;
      } else {
        o = _logCE * x + _logCF;
      }
      return o;
    }

    private static Vector3 LinearToLogC(Vector3 x) {
      return new Vector3(
          LinearToLogCScalar(x.x),
          LinearToLogCScalar(x.y),
          LinearToLogCScalar(x.z)
      );
    }

    private static float LogCToLinearScalar(float x) {
      float o;
      if (x > _logCE * _logCCut + _logCF) {
        o = (Mathf.Pow(10.0f, (x - _logCD) / _logCC) - _logCB) / _logCA;
      } else {
        o = (x - _logCF) / _logCE;
      }
      return o;
    }

    private static Vector3 LogCToLinear(Vector3 x) {
      return new Vector3(
          LogCToLinearScalar(x.x),
          LogCToLinearScalar(x.y),
          LogCToLinearScalar(x.z)
      );
    }

    private static Vector3 SampleVector3(List<Vector3> values, Vector3 index, int length) {
      Vector3 rawPos = index * (length - 1);
      Vector3Int coarsePos = UnityUtils.FloorToInt(rawPos);
      Vector3 finePos = rawPos - coarsePos;

      Vector3 minXMinYMinZ = SampleList(values, ClampLength(coarsePos.x, length) + ClampLength(coarsePos.y, length) * length + ClampLength(coarsePos.z, length) * length * length);
      Vector3 maxXMinYMinZ = SampleList(values, ClampLength((coarsePos.x + 1), length) + ClampLength(coarsePos.y, length) * length + ClampLength(coarsePos.z, length) * length * length);
      Vector3 minXMaxYMinZ = SampleList(values, ClampLength(coarsePos.x, length) + ClampLength((coarsePos.y + 1), length) * length + ClampLength(coarsePos.z, length) * length * length);
      Vector3 maxXMaxYMinZ = SampleList(values, ClampLength((coarsePos.x + 1), length) + ClampLength((coarsePos.y + 1), length) * length + ClampLength(coarsePos.z, length) * length * length);
      Vector3 minXMinYMaxZ = SampleList(values, ClampLength(coarsePos.x, length) + ClampLength(coarsePos.y, length) * length + ClampLength((coarsePos.z + 1), length) * length * length);
      Vector3 maxXMinYMaxZ = SampleList(values, ClampLength((coarsePos.x + 1), length) + ClampLength(coarsePos.y, length) * length + ClampLength((coarsePos.z + 1), length) * length * length);
      Vector3 minXMaxYMaxZ = SampleList(values, ClampLength(coarsePos.x, length) + ClampLength((coarsePos.y + 1), length) * length + ClampLength((coarsePos.z + 1), length) * length * length);
      Vector3 maxXMaxYMaxZ = SampleList(values, ClampLength((coarsePos.x + 1), length) + ClampLength((coarsePos.y + 1), length) * length + ClampLength((coarsePos.z + 1), length) * length * length);
 
      Vector3 minXMinY = UnityUtils.Lerp(minXMinYMinZ, minXMinYMaxZ, finePos.z);
      Vector3 minXMaxY = UnityUtils.Lerp(minXMaxYMinZ, minXMaxYMaxZ, finePos.z);
      Vector3 maxXMinY = UnityUtils.Lerp(maxXMinYMinZ, maxXMinYMaxZ, finePos.z);
      Vector3 maxXMaxY = UnityUtils.Lerp(maxXMaxYMinZ, maxXMaxYMaxZ, finePos.z);
      Vector3 minX = UnityUtils.Lerp(minXMinY, minXMaxY, finePos.y);
      Vector3 maxX = UnityUtils.Lerp(maxXMinY, maxXMaxY, finePos.y);
      return UnityUtils.Lerp(minX, maxX, finePos.x);
    }

    private static Color32 ToColor32(Vector3 value) {
      return new Color32(UnityUtils.Clamp8(Mathf.RoundToInt(0xFF * value.x)),
                         UnityUtils.Clamp8(Mathf.RoundToInt(0xFF * value.y)),
                         UnityUtils.Clamp8(Mathf.RoundToInt(0xFF * value.z)),
                         0xFF);
    }

    private static float SampleFloat(List<float> values, float index, int length, int offset) {
      float rawPos = index * (length - 1);
      int coarsePos = Mathf.FloorToInt(rawPos);
      float finePos = rawPos - coarsePos;
    
      float sampleA = SampleList(values, ClampLength(coarsePos, length) + offset);
      float sampleB = SampleList(values, ClampLength(coarsePos + 1, length) + offset);
      return UnityUtils.Lerp(sampleA, sampleB, finePos);
    }

    private static int ClampLength(int index, int length) {
      return Mathf.Max(0, Mathf.Min(length - 1, index));
    }

    private static T SampleList<T>(List<T> values, int index) {
      if (index <= 0) {
        return values[0];
      }
      if (index >= values.Count) {
        return values[values.Count - 1];
      }
      return values[index];
    }

    private static bool TryGetTaggedContent(string line, string tag, out string rest) {
      if (!line.StartsWith(tag)) {
        rest = null;
        return false;
      }
      rest = line.Substring(tag.Length).Trim();
      return true;
    }

    private static int? ParseInt(string str) {
      if (int.TryParse(str, out int value)) {
        return value;
      }
      return null;
    }

    private static float? ParseFloat(string str) {
      if (float.TryParse(str, out float value)) {
        return value;
      }
      return null;
    }

    private static Vector3? ParseVector3(string str) {
      string[] parts = str.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length != 3) {
        return null;
      }
      if (!float.TryParse(parts[0], out float x) || !float.TryParse(parts[1], out float y) || !float.TryParse(parts[2], out float z)) {
        return null;
      }
      return new Vector3(x, y, z);
    }
  }
}
