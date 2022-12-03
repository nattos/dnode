using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace DNode {
  [RequireComponent(typeof(ScriptMachine))]
  public class DScriptMachine : MonoBehaviour {
    private static readonly IReadOnlyDictionary<MeshGlyphFont.ScriptType, MeshGlyphFont.FontFamily> _fallbackFontFamilies = new Dictionary<MeshGlyphFont.ScriptType, MeshGlyphFont.FontFamily> {
      // Latin
      { MeshGlyphFont.ScriptType.Latin, new MeshGlyphFont.FontFamily(new[] { "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }) },
      // Arabic
      { MeshGlyphFont.ScriptType.Arabic, new MeshGlyphFont.FontFamily(new[] { "Geeza Pro", "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }) },
      // Japanese
      { MeshGlyphFont.ScriptType.Japanese, new MeshGlyphFont.FontFamily(new[] { "ヒラギノ角ゴ Pro W3", "Hiragino Kaku Gothic Pro", "Osaka", "メイリオ", "Meiryo", "ＭＳ Ｐゴシック", "MS PGothic", "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }) },
      // Korean
      { MeshGlyphFont.ScriptType.Korean, new MeshGlyphFont.FontFamily(new[] { "Apple SD Gothic Neo", "NanumBarunGothic", "맑은 고딕", "Malgun Gothic", "굴림", "Gulim", "돋움", "Dotum", "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }) },
      // Russian
      { MeshGlyphFont.ScriptType.Russian, new MeshGlyphFont.FontFamily(new[] { "Charcoal", "Geneva", "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }) },
      // Chinese
      { MeshGlyphFont.ScriptType.Chinese, new MeshGlyphFont.FontFamily(new[] { "华文细黑", "STXihei", "PingFang TC", "微软雅黑体", "Microsoft YaHei New", "微软雅黑", "Microsoft Yahei", "宋体", "SimSun", "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }) },
    };

    private static readonly IReadOnlyList<MeshGlyphFont.ScriptType> _fallbackScriptTypes = new[] {
      MeshGlyphFont.ScriptType.Latin,
      MeshGlyphFont.ScriptType.Japanese,
      MeshGlyphFont.ScriptType.Korean,
      MeshGlyphFont.ScriptType.Chinese,
      MeshGlyphFont.ScriptType.Russian,
      MeshGlyphFont.ScriptType.Arabic,
    };

    private static string _fontCachePath => Path.Combine(Application.temporaryCachePath, "dnode_font_cache.json");

    public MeshGlyphFont.FontFallback DefaultFontFallback;

    public DEnvironmentOverrides Environment;

    public Camera GlobalCamera;
    public Camera CompositingCamera;
    public Camera SceneCamera {
      get {
  #if UNITY_EDITOR
        return UnityEditor.SceneView.GetAllSceneCameras().FirstOrDefault();
  #else // UNITY_EDITOR
        return null;
  #endif // UNITY_EDITOR
      }
    }
    public EnvironmentComponent EnvironmentComponent;
    public MeshGlyphFont DefaultFont;
    public string DefaultFontName;

    public Transport Transport = new Transport();
    public PrefabCache PrefabCache = new PrefabCache();
    public MeshGlyphCache MeshGlyphCache = new MeshGlyphCache();
    public RenderTextureCache RenderTextureCache = new RenderTextureCache();
    public OscManager OscManager = new OscManager();

    public Action delayCallToOnEndFrame;

    private bool _hadRenderToTexture = false;
    public static DScriptMachine CurrentInstance;

    public bool RunOnStep = true;
    public int ReportedOutputNodeCount = 0;
    public int ReportedRenderTextureCount = 0;

    private IFrameComponent[] _frameComponents;

    protected void OnEnable() {
      bool loadedCachedFonts = false;
      string fontCachePath = _fontCachePath;
      if (File.Exists(fontCachePath)) {
        try {
          using (var stream = File.OpenRead(fontCachePath)) {
            MeshGlyphCache.LoadSerializedFontCache(stream);
          }
          DefaultFont = MeshGlyphCache.GetFontByNameImmediate(DefaultFontName);
          DefaultFontFallback = MeshGlyphFont.FontFallback.CreateFallbackImmediate(MeshGlyphCache, _fallbackScriptTypes, _fallbackFontFamilies);
          Debug.Log("Loaded font cache.");
        } catch (Exception e) {
          Debug.Log(e);
        }
      }
      if (!loadedCachedFonts) {
        DefaultFontFallback = MeshGlyphFont.FontFallback.CreateEmptyFallback(MeshGlyphCache);
      }
      Task.Run(async () => {
        var fallbackFont = await MeshGlyphFont.FontFallback.CreateFallbackAsync(MeshGlyphCache, _fallbackScriptTypes, _fallbackFontFamilies);
        try {
          using (var stream = File.OpenWrite(fontCachePath)) {
            MeshGlyphCache.SerializeFontCache(stream);
            Debug.Log("Saved font cache.");
          }
        } catch (Exception e) {
          Debug.Log(e);
        }
        // Delegate back to the main thread, to avoid any cached reads.
        UnityEditor.EditorApplication.delayCall += () => {
          DefaultFontFallback = fallbackFont;
          DefaultFont = MeshGlyphCache.GetFontByNameImmediate(DefaultFontName);
          MeshGlyphCache.PrewarmCache(DefaultFont);
          // Note: Do _not_ prewarm all fallback fonts. This results in a big chunk of time and memory spent on glyphs that will never be used.
          // DefaultFontFallback.PrewarmCache();
        };
      });

      _frameComponents = new IFrameComponent[] {
          PrefabCache,
          EnvironmentComponent,
          RenderTextureCache,
          OscManager,
      }.Concat(GlobalCamera.GetComponentsInChildren<IFrameComponent>()).ToArray();
    }

    protected void OnDisable() {
      foreach (IFrameComponent component in _frameComponents) {
        component.Dispose();
      }
      Action wasDelayCallToOnEndFrame = delayCallToOnEndFrame;
      wasDelayCallToOnEndFrame?.Invoke();
    }

    public void ExportFrameData(DFrameData frameData) {
      DFrameNodes nodes = frameData.Nodes;
      DFrameTexture backgroundTexture = null;
      foreach (DFrameNode node in nodes.Nodes ?? Array.Empty<DFrameNode>()) {
        if (node is DFrameTexture texture) {
          backgroundTexture = texture;
        }
      }

      if (_hadRenderToTexture) {
        GlobalCamera.targetDisplay = 1;
        CompositingCamera.gameObject.SetActive(true);
        CompositingCamera.targetDisplay = 0;

        CompositingCamera.GetComponent<UnityEngine.Rendering.Volume>().profile.TryGet<BlitTexturePostProcessComponent>(out var blitTexturePost);
        blitTexturePost.InputTexture.value = backgroundTexture;
      } else {
        GlobalCamera.targetDisplay = 0;
        GlobalCamera.targetTexture = null;
        CompositingCamera.gameObject.SetActive(false);
        CompositingCamera.targetDisplay = 1;
        RenderToScreen(nodes);
      }
    }

    private void RenderToScreen(DFrameNodes nodes) {
      SetupGlobalCameraForScene(nodes);
    }

    private void SetupGlobalCameraForScene(DFrameNodes nodes) {
      bool hadGlobalCamera = false;
      DFrameTexture backgroundTexture = null;
      foreach (DFrameNode node in nodes.Nodes ?? Array.Empty<DFrameNode>()) {
        if (node is DFrameTexture texture) {
          backgroundTexture = texture;
        } else if (node is DFrameObject fobject) {
          if (fobject.GameObject == GlobalCamera.OrNull()?.gameObject) {
            hadGlobalCamera = true;
          }
        }
      }

      if (backgroundTexture != null) {
        EnvironmentComponent.BackgroundTexture.Value = backgroundTexture.Texture;
        EnvironmentComponent.BackgroundTextureAlpha.Value = 1.0f;
      }
      if (!hadGlobalCamera && SceneCamera && GlobalCamera) {
        GlobalCamera.transform.position = SceneCamera.transform.position;
        GlobalCamera.transform.rotation = SceneCamera.transform.rotation;
      }
    }

    public Texture RenderNodesToTexture(DFrameNodes nodes, TextureSizeSource sizeSource) {
      SetupGlobalCameraForScene(nodes);

      RenderTexture outputTexture = RenderTextureCache.Allocate(RenderTextureCache.GetSizeFromSource(Environment.EffectiveOutputSize, sizeSource));
      //RenderTexture oldOutputTexture = GlobalCamera.targetTexture;
      GlobalCamera.targetTexture = outputTexture;
      GlobalCamera.pixelRect = new Rect(0, 0, outputTexture.width, outputTexture.height);
      GlobalCamera.Render();
      // Note: Do not reset the camera's render texture. This is so that it retains the correct output pixel
      // size, and that it's world to screen matrices remain correct. This is ok, as long as we are careful
      // to always set Camera.targetTexture before calling Camera.Render.
      //GlobalCamera.targetTexture = oldOutputTexture;

      _hadRenderToTexture = true;
      return outputTexture;
    }

    public bool PreviousSlideRequested = false;
    public bool NextSlideRequested = false;

    protected void Update() {
      PreviousSlideRequested =
          UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.UpArrow) ||
          UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow);
      NextSlideRequested =
          UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.DownArrow) ||
          UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow);
      if (!RunOnStep) {
        return;
      }
      StepFrame();
    }

    private void StepFrame() {
      var previousInstance = CurrentInstance;
      CurrentInstance = this;

      Camera gameViewCamera = CompositingCamera.targetDisplay == 0 ? CompositingCamera : GlobalCamera;
      RenderTextureCache.ScreenWidth = gameViewCamera.pixelWidth;
      RenderTextureCache.ScreenHeight = gameViewCamera.pixelHeight;
      _hadRenderToTexture = false;
      ReportedRenderTextureCount = RenderTextureCache.AllocatedCount;
      ++Transport.AbsoluteFrame;
      foreach (IFrameComponent component in _frameComponents) {
        component.OnStartFrame();
      }
      DStepEvent.Trigger(this);
      foreach (IFrameComponent component in _frameComponents) {
        component.OnEndFrame();
      }
      Action wasDelayCallToOnEndFrame = delayCallToOnEndFrame;
      wasDelayCallToOnEndFrame?.Invoke();

      CurrentInstance = previousInstance;
    }

    public static void DelayCall(Action handler) {
      if (CurrentInstance) {
        CurrentInstance.delayCallToOnEndFrame += handler;
        return;
      }
  #if UNITY_EDITOR
      bool isEditor = true;
      if (isEditor) {
        // Support calling these methods during serialization.
        UnityEditor.EditorApplication.delayCall += () => handler.Invoke();
        return;
      }
  #endif // UNITY_EDITOR
      handler.Invoke();
    }
  }
}
