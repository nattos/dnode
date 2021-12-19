using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DNode {
  public static class UnityUtils {
    public const double DefaultEpsilon = 1e-6;

    private static Texture3D _blankTexture3D;
    public static Texture3D BlankTexture3D {
      get {
        if (!_blankTexture3D) {
          _blankTexture3D = new Texture3D(1, 1, 1, TextureFormat.RGBA32, mipChain: false);
        }
        return _blankTexture3D;
      }
    }

    private static Texture2D _blankTexture;
    public static Texture2D BlankTexture {
      get {
        if (!_blankTexture) {
          _blankTexture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
        }
        return _blankTexture;
      }
    }

    public static T OrNull<T>(this T value) where T : UnityEngine.Object {
      return value == null ? null : value;
    }

    public static void Destroy(UnityEngine.Object obj) {
  #if UNITY_EDITOR
      if (!UnityEditor.EditorApplication.isPlaying) {
        UnityEngine.Object.DestroyImmediate(obj);
        return;
      }
  #endif // UNITY_EDITOR
      UnityEngine.Object.Destroy(obj);
    }

    private static readonly List<Renderer> _staticGetLocalMeshBoundsRendererList = new List<Renderer>();

    public static (Vector3 min, Vector3 max) GetWorldMeshBoundsMinMax(GameObject gameObject) {
      Vector3? totalWorldMin = null;
      Vector3? totalWorldMax = null;
      gameObject.GetComponentsInChildren<Renderer>(_staticGetLocalMeshBoundsRendererList);
      foreach (Renderer renderer in _staticGetLocalMeshBoundsRendererList) {
        Bounds localBounds = renderer.localBounds;
        Vector3 localMin = localBounds.min;
        Vector3 localMax = localBounds.max;
        Transform localTransform = renderer.transform;
        Vector3 worldMin = localTransform.TransformPoint(localMin);
        Vector3 worldMax = localTransform.TransformPoint(localMax);
        totalWorldMin = Vector3.Min(worldMin, totalWorldMin ?? worldMin);
        totalWorldMax = Vector3.Min(worldMax, totalWorldMax ?? worldMax);
      }
      return (totalWorldMin ?? Vector3.zero, totalWorldMax ?? Vector3.zero);
    }

    private static readonly Vector3 kRGBToYPrime = new Vector3(0.299f, 0.587f, 0.114f);
    private static readonly Vector3 kRGBToI = new Vector3(0.596f, -0.275f, -0.321f);
    private static readonly Vector3 kRGBToQ = new Vector3(0.212f, -0.523f, 0.311f);

    private static readonly Vector3 kYIQToR = new Vector3(1.0f, 0.956f, 0.621f);
    private static readonly Vector3 kYIQToG = new Vector3(1.0f, -0.272f, -0.647f);
    private static readonly Vector3 kYIQToB = new Vector3(1.0f, -1.107f, 1.704f);

    public static Vector4 ToHslRadians(Color color) {
      Vector3 colorVec = new Vector3(color.r, color.g, color.b);
      float YPrime = Vector3.Dot(colorVec, kRGBToYPrime);
      float I = Vector3.Dot(colorVec, kRGBToI);
      float Q = Vector3.Dot(colorVec, kRGBToQ);
      float hue = Mathf.Abs(I) <= (1.0f / (256 * 256)) ? 0.0f : Mathf.Atan2(Q, I);
      float chroma = Mathf.Sqrt(I * I + Q * Q);
      return new Vector4(hue, chroma, YPrime, color.a);
    }

    public static Vector4 ToHsl(Color color) {
      Vector4 hsl = ToHslRadians(color);
      hsl.x *= Mathf.Rad2Deg;
      return hsl;
    }

    public static Color FromHsl(Vector4 hsl) {
      hsl.x *= Mathf.Deg2Rad;
      return FromHslRadians(hsl);
    }

    public static Color FromHslRadians(Vector4 hsl) {
      float hue = hsl.x;
      float chroma = hsl.y;
      float lightness = hsl.z;
      float Q = chroma * Mathf.Sin(hue);
      float I = chroma * Mathf.Cos(hue);
      Vector3 yIQ = new Vector3(lightness, I, Q);
      return new Color(Vector3.Dot(yIQ, kYIQToR), Vector3.Dot(yIQ, kYIQToG), Vector3.Dot(yIQ, kYIQToB), hsl.w);
    }

    public static Color LerpAlpha(Color color, float targetAlpha, float t) {
      return new Color(color.r, color.g, color.b, Lerp(color.a, targetAlpha, t));
    }
  
    public static int Modulo(int a, int b) {
      int remainder = a % b;
      return remainder < 0 ? (b + remainder) : remainder;
    }

    public static double WrappedModulo(double a, double b) {
      if (a < 0) {
        return (b - ((-a) % b)) % b;
      } else {
        return a % b;
      }
    }

    public static float Lerp(float a, float b, float t) {
      return a * (1.0f - t) + b * t;
    }

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) {
      return a * (1.0f - t) + b * t;
    }
  
    public static Vector2 Lerp(Vector2 a, Vector2 b, Vector2 t) {
      return a.ElementMul(Vector2.one - t) + b.ElementMul(t);
    }

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) {
      return a * (1.0f - t) + b * t;
    }
  
    public static Vector3 Lerp(Vector3 a, Vector3 b, Vector3 t) {
      return a.ElementMul(Vector3.one - t) + b.ElementMul(t);
    }
  
    public static double Lerp(double a, double b, double t) {
      return a * (1.0 - t) + b * t;
    }

    public static double Clamp(double value) {
      return Math.Max(0.0, Math.Min(1.0, value));
    }

    public static double Smoothstep(double x) {
      return x * x * (3.0 - 2.0 * x);
    }

    public static Vector2 ElementMul(this Vector2 lhs, Vector2 rhs) {
      return new Vector2(lhs.x * rhs.x, lhs.y * rhs.y);
    }

    public static Vector2 ElementDiv(this Vector2 lhs, Vector2 rhs) {
      return new Vector2(lhs.x / rhs.x, lhs.y / rhs.y);
    }

    public static Vector3 ElementMul(this Vector3 lhs, Vector3 rhs) {
      return new Vector3(lhs.x * rhs.x, lhs.y * rhs.y, lhs.z * rhs.z);
    }

    public static Vector3 ElementDiv(this Vector3 lhs, Vector3 rhs) {
      return new Vector3(lhs.x / rhs.x, lhs.y / rhs.y, lhs.z / rhs.z);
    }

    public static Vector3Int FloorToInt(Vector3 value) {
      return new Vector3Int(Mathf.FloorToInt(value.x), Mathf.FloorToInt(value.y), Mathf.FloorToInt(value.z));
    }

    public static byte Clamp8(int value) {
      return (byte)Mathf.Clamp(value, 0, 0xFF);
    }

    public static bool TryConvert<T>(object value, out T outValue, bool guaranteed) {
      // Note: Bug in ConversionUtility.TryConvert<T> prevents us from using it (it always returns false) (Case 1377071).
      bool result = Unity.VisualScripting.ConversionUtility.TryConvert(value, typeof(T), out object objectValue, guaranteed);
      outValue = result ? (T)objectValue : default;
      return result;
    }
  }
}
