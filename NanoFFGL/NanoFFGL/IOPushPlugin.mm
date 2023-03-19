//#include "Prefix.pch"

#import "Foundation/Foundation.h"
#import "simd/simd.h"
#import "MetalKit/MetalKit.h"
#import "CoreVideo/CoreVideo.h"

#import "AAPLOpenGLMetalInteropTexture.h"

#include <dlfcn.h>
#include <algorithm>
#include <memory>
#include <string>
#include <stdexcept>
#include <unordered_map>

#include <string>
#include <FFGLSDK.h>

#include "NioStackData.h"



#if NIO_PUSH
  #define PLUGIN_CODE "nIOp"
  #define PLUGIN_NAME "nIO Push"
  #define NIO_HAS_STORED_TEXTURE 1
  #define NIO_TRACKS_ZOMBIE_TEXTURES 1
#endif // NIO_PUSH

#if NIO_POP
  #define PLUGIN_CODE "nIOP"
  #define PLUGIN_NAME "nIO Pop"
#endif // NIO_POP

#if NIO_EXPORT
  #define PLUGIN_CODE "nIOE"
  #define PLUGIN_NAME "nIO Export"
  #define NIO_HAS_STORED_TEXTURE 1
  #define NIO_USES_SYPHON 1
#endif // NIO_EXPORT





#if NIO_USES_SYPHON
  #import "Syphon/Syphon_Prefix.pch"
  #import "Syphon/SyphonServerConnectionManager.h"
#endif // NIO_USES_SYPHON




using namespace ffglex;


static const char _blitFromRectVertexShaderCode[] = R"(#version 410 core
uniform vec2 MaxUV;

layout(location = 0) in vec4 vPosition;
layout(location = 1) in vec2 vUV;

out vec2 uv;

void main() {
  gl_Position = vPosition;
  uv = vUV;
  uv = uv * MaxUV;
}
)";

static const char _blitFromRectFragmentShaderCode[] = R"(#version 410 core
uniform sampler2DRect InputTexture;
uniform vec3 Brightness;

in vec2 uv;

out vec4 fragColor;

void main() {
  fragColor = texture(InputTexture, uv);
}
)";

static const char _blitFromTex2DVertexShaderCode[] = R"(#version 410 core
uniform vec2 MaxUV;

layout(location = 0) in vec4 vPosition;
layout(location = 1) in vec2 vUV;

out vec2 uv;

void main() {
  gl_Position = vPosition;
  uv = vUV;
  uv = uv * MaxUV;
}
)";

static const char _blitFromTex2DFragmentShaderCode[] = R"(#version 410 core
uniform sampler2D InputTexture;
uniform vec3 Brightness;

in vec2 uv;

out vec4 fragColor;

void main() {
  fragColor = texture(InputTexture, uv);
}
)";



inline FFGLTexCoords GetMaxGLTexCoordsRect(FFGLTextureStruct t) {
  FFGLTexCoords texCoords;

  //the texture may only occupy a portion
  //of the allocated hardware texture memory

  //Non-normalized (0..1) S and T coords
  texCoords.s = ((GLfloat)t.Width);
  texCoords.t = ((GLfloat)t.Height);

  return texCoords;
}

class Pipeline {
public:
  Pipeline() {
    _device = MTLCreateSystemDefaultDevice();
    _blitFromRectShader.Compile(_blitFromRectVertexShaderCode, _blitFromRectFragmentShaderCode);
    _blitFromTex2DShader.Compile(_blitFromTex2DVertexShaderCode, _blitFromTex2DFragmentShaderCode);
    _fullViewportQuad.Initialise();
  }
  ~Pipeline() {
    _blitFromRectShader.FreeGLResources();
    _blitFromTex2DShader.FreeGLResources();
    _fullViewportQuad.Release();
  }

  id<MTLDevice> GetDevice() const { return _device; }

  AAPLOpenGLMetalInteropTexture* CreateInteropTexture(int width, int height, bool createFbo = false) const {
    return [[AAPLOpenGLMetalInteropTexture alloc] initWithMetalDevice:_device openGLContext:[NSOpenGLContext currentContext] createOpenGLFBO:createFbo metalPixelFormat:MTLPixelFormatBGRA8Unorm width:width height:height];
  }

  ffglex::FFGLShader& GetBlitFromRectShader() { return _blitFromRectShader; }
  ffglex::FFGLShader& GetBlitFromTex2DShader() { return _blitFromTex2DShader; }
  ffglex::FFGLScreenQuad& GetFullViewportQuad() { return _fullViewportQuad; }

private:
  id<MTLDevice> _device;
  ffglex::FFGLShader _blitFromRectShader;
  ffglex::FFGLShader _blitFromTex2DShader;
  ffglex::FFGLScreenQuad _fullViewportQuad;
};


std::unique_ptr<Pipeline> g_pipeline;








//static void Log(NSString* message) {
//  NSFileHandle *fileHandle = [NSFileHandle fileHandleForWritingAtPath:@"/Users/nattos/Desktop/log.txt"];
//  [fileHandle seekToEndOfFile];
//  [fileHandle writeData:[message dataUsingEncoding:NSUTF8StringEncoding]];
//  [fileHandle closeFile];
//}



#define IOPushPluginPlaceholderClass IOPushPluginPlaceholder_##PLUGIN_CODE

@interface IOPushPluginPlaceholderClass : NSObject
@end
@implementation IOPushPluginPlaceholderClass
@end


static NioGetStackDataPtr g_NioGetStackDataPtr = nullptr;
static NioGetStackDataEntryCountPtr g_NioGetStackDataEntryCountPtr = nullptr;
static NioGetStackDataEntryPtr g_NioGetStackDataEntryPtr = nullptr;
static NioAddStackDataEntryPtr g_NioAddStackDataEntryPtr = nullptr;
static NioRemoveStackDataEntryPtr g_NioRemoveStackDataEntryPtr = nullptr;

static NioStackDataHandle NioGetStackDataDynamic() {
  if (!g_NioGetStackDataPtr) {
    NSBundle* bundle = [NSBundle bundleForClass:[IOPushPluginPlaceholderClass class]];
    NSString* bundlePath = [bundle bundlePath];
    NSString* bundleDir = [bundlePath stringByDeletingLastPathComponent];
    NSString* libraryPath = [bundleDir stringByAppendingPathComponent:@"libNioStack.dylib"];

    void* libraryHandle = dlopen([libraryPath cStringUsingEncoding:NSUTF8StringEncoding], 0);
    if (!libraryHandle) {
      return nullptr;
    }
    g_NioGetStackDataPtr = (NioGetStackDataPtr)dlsym(libraryHandle, "NioGetStackData");
    g_NioGetStackDataEntryCountPtr = (NioGetStackDataEntryCountPtr)dlsym(libraryHandle, "NioGetStackDataEntryCount");
    g_NioGetStackDataEntryPtr = (NioGetStackDataEntryPtr)dlsym(libraryHandle, "NioGetStackDataEntry");
    g_NioAddStackDataEntryPtr = (NioAddStackDataEntryPtr)dlsym(libraryHandle, "NioAddStackDataEntry");
    g_NioRemoveStackDataEntryPtr = (NioRemoveStackDataEntryPtr)dlsym(libraryHandle, "NioRemoveStackDataEntry");

    if (!g_NioGetStackDataPtr) {
      return nullptr;
    }
  }
  return g_NioGetStackDataPtr();
}






class PluginImpl : public CFFGLPlugin {
public:
  PluginImpl() {
    SetMinInputs(1);
    SetMaxInputs(1);
  }
  virtual ~PluginImpl() {}

  FFResult InitGL(const FFGLViewportStruct* vp) override {
    if (!g_pipeline) {
      g_pipeline.reset(new Pipeline());
    }
    return CFFGLPlugin::InitGL(vp);
  }

  FFResult DeInitGL() override {
#if NIO_HAS_STORED_TEXTURE
    _storedTexture = nullptr;
#endif // NIO_HAS_STORED_TEXTURE

#if NIO_USES_SYPHON
  ReleaseSyphonConnection();
#endif // NIO_USES_SYPHON

#if NIO_PUSH
    NioStackDataHandle data = NioGetStackDataDynamic();
    if (!data) {
      return FF_FAIL;
    }
    {
      int entryCount = g_NioGetStackDataEntryCountPtr(data);
      for (int entryIndex = entryCount - 1; entryIndex >= 0; --entryIndex) {
        NioStackEntry* entry = g_NioGetStackDataEntryPtr(data, entryIndex);
        if (entry->Source != this) {
          continue;
        }
        g_NioRemoveStackDataEntryPtr(data, entryIndex);
      }
    }
#endif // NIO_PUSH


    return FF_SUCCESS;
  }

  FFResult ProcessOpenGL(ProcessOpenGLStruct* pGL) override {
    if (pGL->numInputTextures < 1) {
      return FF_FAIL;
    }
    if (pGL->inputTextures[0] == NULL) {
      return FF_FAIL;
    }
    NioStackDataHandle data = NioGetStackDataDynamic();
    if (!data) {
      return FF_FAIL;
    }

    // Blit OpenGL input texture over to Metal.
    const auto* pInputTexture = pGL->inputTextures[0];
#if NIO_HAS_STORED_TEXTURE
    _storedTexture = ResizeTexture(_storedTexture, pInputTexture->HardwareWidth, pInputTexture->HardwareHeight, /* createGLFBO */ true);
#endif // NIO_HAS_STORED_TEXTURE

#if NIO_TRACKS_ZOMBIE_TEXTURES
    {
      // Note: If the rendering thread changes rapidly this breaks.
      std::vector<int> toErase;
      int entryIndex = 0;
      int entryCount = g_NioGetStackDataEntryCountPtr(data);
      for (int entryIndex = 0; entryIndex < entryCount; ++entryIndex) {
        NioStackEntry* entry = g_NioGetStackDataEntryPtr(data, entryIndex);
        if (entry->Source == this) {
          continue;
        }
        auto findIt = _lastSeenFrameCounts.find(entry->Source);
        if (findIt == _lastSeenFrameCounts.end()) {
          _lastSeenFrameCounts[entry->Source] = entry->LastFrameNumber;
        } else {
          int lastSeenFrameCount = findIt->second;
          if (std::abs(lastSeenFrameCount - entry->LastFrameNumber) > 1) {
            toErase.push_back(entryIndex);
            _lastSeenFrameCounts.erase(findIt);
          } else {
            findIt->second = entry->LastFrameNumber;
          }
        }
      }
      if (toErase.size() > 0) {
        for (auto it = toErase.rbegin(); it != toErase.rend(); ++it) {
          int toEraseIndex = *it;
          g_NioRemoveStackDataEntryPtr(data, toEraseIndex);
        }
      }
    }
#endif // NIO_TRACKS_ZOMBIE_TEXTURES

#if NIO_PUSH
    {
      auto& shader = g_pipeline->GetBlitFromTex2DShader();
      ScopedFBOBinding fboBinding(_storedTexture.openGLFBO, ScopedFBOBinding::RB_REVERT);
      ScopedShaderBinding shaderBinding(shader.GetGLID());
      ScopedSamplerActivation activateSampler(0);
      ScopedTextureBinding textureBinding(GL_TEXTURE_2D, pInputTexture->Handle);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MAG_FILTER, GL_NEAREST);

      shader.Set("InputTexture", 0);
      FFGLTexCoords maxCoords = GetMaxGLTexCoords(*pGL->inputTextures[0]);
      shader.Set("MaxUV", maxCoords.s, maxCoords.t);
      g_pipeline->GetFullViewportQuad().Draw();

      ++_frameNumber;

      NioStackEntry* thisEntry = nullptr;
      int entryIndex = 0;
      int entryCount = g_NioGetStackDataEntryCountPtr(data);
      for (int entryIndex = 0; entryIndex < entryCount; ++entryIndex) {
        NioStackEntry* entry = g_NioGetStackDataEntryPtr(data, entryIndex);
        if (entry->Source != this) {
          continue;
        }
        thisEntry = entry;
        break;
      }
      if (!thisEntry) {
        thisEntry = g_NioAddStackDataEntryPtr(data);
        thisEntry->Source = this;
      }
      thisEntry->LastFrameNumber = _frameNumber;
      thisEntry->TextureRef = _storedTexture;
      thisEntry->OpenGLTexture = _storedTexture.openGLTexture;
    }
    // Return fail so that we can avoid a texture copy. The host _should_ just ignore our output.
    return FF_FAIL;
#endif // NIO_PUSH
#if NIO_POP
    // Blit result back to OpenGL output texture.
    {
      int dataEntryCount = g_NioGetStackDataEntryCountPtr(data);
      if (dataEntryCount <= 0) {
        return FF_FAIL;
      }
      NioStackEntry* entry = g_NioGetStackDataEntryPtr(data, dataEntryCount - 1);

      auto& shader = g_pipeline->GetBlitFromRectShader();
      ScopedShaderBinding shaderBinding(shader.GetGLID());
      ScopedSamplerActivation activateSampler(0);
      ScopedTextureBinding textureBinding(GL_TEXTURE_RECTANGLE, entry->OpenGLTexture);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MAG_FILTER, GL_NEAREST);

      shader.Set("InputTexture", 0);
      FFGLTexCoords maxCoords = GetMaxGLTexCoordsRect(*pGL->inputTextures[0]);
      shader.Set("MaxUV", maxCoords.s, maxCoords.t);
      g_pipeline->GetFullViewportQuad().Draw();

      g_NioRemoveStackDataEntryPtr(data, dataEntryCount - 1);
    }
    return FF_SUCCESS;
#endif // NIO_POP
#if NIO_EXPORT
    {
      auto& shader = g_pipeline->GetBlitFromTex2DShader();
      ScopedFBOBinding fboBinding(_storedTexture.openGLFBO, ScopedFBOBinding::RB_REVERT);
      ScopedShaderBinding shaderBinding(shader.GetGLID());
      ScopedSamplerActivation activateSampler(0);
      ScopedTextureBinding textureBinding(GL_TEXTURE_2D, pInputTexture->Handle);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MAG_FILTER, GL_NEAREST);

      shader.Set("InputTexture", 0);
      FFGLTexCoords maxCoords = GetMaxGLTexCoords(*pGL->inputTextures[0]);
      shader.Set("MaxUV", maxCoords.s, maxCoords.t);
      g_pipeline->GetFullViewportQuad().Draw();

      ++_frameNumber;
    }
    CheckSyphonConnection(pInputTexture->Width, pInputTexture->Height);
    if (_ioSurfaceTexture) {
      glFlush();
      id<MTLCommandQueue> commandQueue = [g_pipeline->GetDevice() newCommandQueue];
      id<MTLCommandBuffer> commandBuffer = [commandQueue commandBuffer];
      id<MTLBlitCommandEncoder> encoder = [commandBuffer blitCommandEncoder];
      [encoder copyFromTexture:_storedTexture.metalTexture
                   sourceSlice:0
                   sourceLevel:0
                  sourceOrigin:MTLOriginMake(0, 0, 0)
                    sourceSize:MTLSizeMake(pInputTexture->Width, pInputTexture->Height, 0)
                     toTexture:_ioSurfaceTexture
              destinationSlice:0
              destinationLevel:0
             destinationOrigin:MTLOriginMake(0, 0, 0)];
      [encoder endEncoding];
      [commandBuffer commit];
      [_syphonConnection publishNewFrame];
    }
    // Return fail so that we can avoid a texture copy. The host _should_ just ignore our output.
    return FF_FAIL;
#endif // NIO_EXPORT
  }

  virtual FFResult SetFloatParameter(uint index, float value) override {
    return FF_FAIL;
  }

  virtual float GetFloatParameter(uint index) override {
    return 0.0f;
  }

private:
#if NIO_USES_SYPHON
  void CheckSyphonConnection(int width, int height) {
    if (_syphonConnection && _syphonSurfaceWidth == width && _syphonSurfaceHeight == height) {
      return;
    }
    ReleaseSyphonConnection();
    _syphonSurfaceWidth = width;
    _syphonSurfaceHeight = height;

    NSDictionary *attribs = @{ (NSString *)kIOSurfaceIsGlobal: @YES,
                               (NSString *)kIOSurfaceWidth: @(width),
                               (NSString *)kIOSurfaceHeight: @(height),
                               (NSString *)kIOSurfaceBytesPerElement: @4u };
    _ioSurface = IOSurfaceCreate((CFDictionaryRef)attribs);

    MTLTextureDescriptor *desc = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm_sRGB
                                                                                    width:width
                                                                                   height:height
                                                                                mipmapped:NO];
    _ioSurfaceTexture = [g_pipeline->GetDevice() newTextureWithDescriptor:desc iosurface:_ioSurface plane:0];

    _syphonUuid = SyphonCreateUUIDString();
    _syphonConnection = [[SyphonServerConnectionManager alloc] initWithUUID:_syphonUuid options:nil];
    [_syphonConnection setSurfaceID:IOSurfaceGetID(_ioSurface)];
    [_syphonConnection start];

    _syphonObserverKey = [NSDistributedNotificationCenter.defaultCenter addObserverForName:SyphonServerAnnounceRequest
                                                                                    object:nullptr
                                                                                     queue:nil
                                                                                usingBlock:^(NSNotification * _Nonnull note) {
      PostSyphonNotification(SyphonServerAnnounce);
    }];
    PostSyphonNotification(SyphonServerAnnounce);
  }

  void ReleaseSyphonConnection() {
    if (_syphonObserverKey) {
      [NSDistributedNotificationCenter.defaultCenter removeObserver:_syphonObserverKey name:SyphonServerAnnounceRequest object:nullptr];
      _syphonObserverKey = nullptr;
    }
    if (_syphonConnection) {
      [_syphonConnection stop];
      _syphonConnection = nullptr;
      PostSyphonNotification(SyphonServerRetire);
    }
    if (_ioSurface) {
      CFRelease(_ioSurface);
      _ioSurface = nullptr;
    }
    _ioSurfaceTexture = nullptr;
    _syphonUuid = nullptr;
    _syphonSurfaceWidth = 0;
    _syphonSurfaceHeight = 0;
  }

  void PostSyphonNotification(NSString* notificationName) {
    NSDictionary *description = GetSyphonDescription();
    [NSDistributedNotificationCenter.defaultCenter postNotificationName:notificationName
                                                                 object:description[SyphonServerDescriptionUUIDKey]
                                                               userInfo:description
                                                     deliverImmediately:YES];
  }

  NSDictionary* GetSyphonDescription() {
    NSDictionary *surface = _syphonConnection.surfaceDescription;
    if (!surface) surface = [NSDictionary dictionary];

    // Getting the app name: helper tasks, command-line tools, etc, don't have a NSRunningApplication instance,
    // so fall back to NSProcessInfo in those cases, then use an empty string as a last resort.
    // http://developer.apple.com/library/mac/qa/qa1544/_index.html
    NSString *appName = [[NSRunningApplication currentApplication] localizedName];
    if (!appName) appName = [[NSProcessInfo processInfo] processName];
    if (!appName) appName = [NSString string];

    return @{ SyphonServerDescriptionDictionaryVersionKey: @kSyphonDictionaryVersion,
              SyphonServerDescriptionNameKey: _syphonServerName,
              SyphonServerDescriptionUUIDKey: _syphonUuid,
              SyphonServerDescriptionAppNameKey: appName,
              SyphonServerDescriptionSurfacesKey: @[ surface ] };
  }

  static NSString* SyphonCreateUUIDString() {
    // generate UUID
    CFUUIDRef	uuidObj = CFUUIDCreate(nil);
    CFStringRef uuid = CFUUIDCreateString(nil, uuidObj);
    CFRelease(uuidObj);
    NSString *result = [[NSString alloc] initWithFormat:@"%@.%@", kSyphonIdentifier, uuid];
    CFRelease(uuid);
    return result;
  }
#endif // NIO_USES_SYPHON

  static AAPLOpenGLMetalInteropTexture* ResizeTexture(AAPLOpenGLMetalInteropTexture* texture, int width, int height, bool createGLFBO = false) {
    if (!texture || texture.width != width || texture.height != height) {
      texture = g_pipeline->CreateInteropTexture(width, height, createGLFBO);
      if (!texture) {
        FFGLLog::LogToHost("Failed to allocate texture.");
      }
    }
    return texture;
  }

  int _frameNumber = 0;
  id<MTLDevice> _device = nullptr;
#if NIO_HAS_STORED_TEXTURE
  AAPLOpenGLMetalInteropTexture* _storedTexture = nullptr;
#endif // NIO_HAS_STORED_TEXTURE
#if NIO_TRACKS_ZOMBIE_TEXTURES
  std::unordered_map<void*, int> _lastSeenFrameCounts;
#endif // NIO_TRACKS_ZOMBIE_TEXTURES

#if NIO_USES_SYPHON
  int _syphonSurfaceWidth = 0;
  int _syphonSurfaceHeight = 0;
  id<NSObject> _syphonObserverKey;
  NSString* _syphonServerName = @"nIO Syphon Export";
  IOSurfaceRef _ioSurface;
  id<MTLTexture> _ioSurfaceTexture;
  NSString* _syphonUuid;
  SyphonServerConnectionManager* _syphonConnection;
#endif // NIO_USES_SYPHON
};


static CFFGLPluginInfo PluginInfo(
  // Create method.
  PluginFactory<PluginImpl>,
  // Plugin unique ID of maximum length 4.
  PLUGIN_CODE,
  // Plugin name.
  PLUGIN_NAME,
  // API major version number.
  2,
  // API minor version number.
  1,
  // Plugin major version number.
  1,
  // Plugin minor version number.
  0,
  // Plugin type.
  FF_EFFECT,
  // Plugin description.
  "",
  // About.
  ""
);



