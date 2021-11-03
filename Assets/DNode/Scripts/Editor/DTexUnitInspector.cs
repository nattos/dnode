using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace DNode {
  [Inspector(typeof(DTexUnit))]
  public class DTexUnitInspector : UnitInspector {
    private const int _textureUsedPeriodMillis = 100;
    private const double _textureUsedTimeout = 1.0;

    private class TextureUsedCheckerState {
      public volatile bool Terminate;
    }

    private const float _previewHeight = 200;

    private DTexUnit _capturingUnit = null;
    private RenderTexture _captureTexture;

    private double _lastDrawTime = Time.realtimeSinceStartupAsDouble;
    private TextureUsedCheckerState _textureUsedCheckerState = null;

    public DTexUnitInspector(Metadata metadata) : base(metadata) {}

    protected override float GetHeight(float width, GUIContent label) {
      return base.GetHeight(width, label) + _previewHeight;
    }

    public override void Initialize() {
      base.Initialize();
    }

    public override void Dispose() {
      base.Dispose();
      StopCapturingTexture();
      DestroyTexture();
    }

    protected override void OnGUI(Rect position, GUIContent label) {
      _lastDrawTime = Time.realtimeSinceStartupAsDouble;
      Rect innerRect = position;
      innerRect.height -= _previewHeight;
      Rect lowerRect = position;
      lowerRect.y = innerRect.yMax;
      lowerRect.height = _previewHeight;
      base.OnGUI(innerRect, label);
      OnPreviewGui(lowerRect);
    }

    private void OnPreviewGui(Rect rect) {
      float topPadding = EditorGUIUtility.singleLineHeight;
      float yPadding = 1.0f;
      float yPos = rect.yMin + topPadding;
      Rect descRect = rect;
      descRect.y = yPos;
      descRect.height = EditorGUIUtility.singleLineHeight;
      yPos = descRect.yMax + yPadding;
      Rect previewRect = rect;
      previewRect.y = yPos;
      previewRect.height = rect.yMax - yPos;

      StartCapturingTexture();
      _capturingUnit?.DebugCaptureTexturePullHandler?.Invoke();
      if (_capturingUnit == null || _captureTexture == null) {
        EditorGUI.LabelField(descRect, $"Texture: waiting to capture");
        return;
      }
      EditorGUI.LabelField(descRect, $"Texture: {_captureTexture.width}x{_captureTexture.height}");
      EditorGUI.DrawPreviewTexture(previewRect, _captureTexture, mat: null, scaleMode: ScaleMode.ScaleToFit);
    }

    private void StartCapturingTexture() {
      StopCapturingTexture();

      _capturingUnit = metadata.value as DTexUnit;
      if (_capturingUnit == null) {
        return;
      }

      _capturingUnit.DebugCaptureTexture = size => {
        CreateTexture(size);
        return _captureTexture;
      };
    }

    private void StopCapturingTexture() {
      if (_capturingUnit == null) {
        return;
      }
      _capturingUnit.DebugCaptureTexture = null;
      _capturingUnit = null;
    }

    private void CreateTexture(Vector2Int size) {
      if (_captureTexture == null || _captureTexture.width != size.x || _captureTexture.height != size.y) {
        UnityUtils.Destroy(_captureTexture);
        _captureTexture = new RenderTexture(size.x, size.y, depth: 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, mipCount: 0);
        _captureTexture.autoGenerateMips = false;
        _captureTexture.filterMode = FilterMode.Point;
        _captureTexture.wrapMode = TextureWrapMode.Clamp;
      }
      BeginCheckingTextureUsed();
    }

    private void DestroyTexture() {
      UnityUtils.Destroy(_captureTexture);
      _captureTexture = null;
      EndCheckingTextureUsed();
    }

    private void BeginCheckingTextureUsed() {
      if (_textureUsedCheckerState != null) {
        return;
      }
      var textureUsedCheckerState = new TextureUsedCheckerState();
      _textureUsedCheckerState = textureUsedCheckerState;
      EditorApplication.delayCall += async () => {
        while (!textureUsedCheckerState.Terminate) {
          if (_captureTexture == null) {
            return;
          }
          if (Time.realtimeSinceStartupAsDouble - _lastDrawTime > _textureUsedTimeout) {
            DestroyTexture();
            return;
          }
          await Task.Delay(_textureUsedPeriodMillis);
        }
      };
    }

    private void EndCheckingTextureUsed() {
      if (_textureUsedCheckerState == null) {
        return;
      }
      _textureUsedCheckerState.Terminate = true;
      _textureUsedCheckerState = null;
    }
  }
}
