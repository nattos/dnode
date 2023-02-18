#include "Prefix.pch"

#import "Foundation/Foundation.h"
#import "simd/simd.h"
#import "MetalKit/MetalKit.h"
#import "CoreVideo/CoreVideo.h"

#import "AAPLOpenGLMetalInteropTexture.h"

#include <memory>
#include <string>
#include <stdexcept>

#include <string>
#include <FFGLSDK.h>

#include "NanoProgram.h"
#include "Generated/Plugin.incl.h"


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


class PluginImpl : public CFFGLPlugin {
public:
  PluginImpl() {
    FinishDeclaration(/* minInputs */ 1, /* maxOutputs */ 1);
    _program.reset(CreateProgram());
    _program->SetupParameters();

    auto valueInputs = _program->GetParameterDecls();
    _parameterIndexMap.resize(valueInputs.size());
    for (int i = 0; i < valueInputs.size(); ++i) {
      int index = i;
      const auto& valueInput = valueInputs[i];
      _parameterIndexMap[i] = valueInput.Key;
      float defaultValue = (float)valueInput.DefaultValue;
      SetParamInfo(index, valueInput.Name.c_str(), FF_TYPE_STANDARD, defaultValue);
      //In FFGLPluginManager.cpp, line 274, SetParamInfo clamps the default value to 0...1 in case of FF_TYPE_STANDARD
      if(defaultValue < 0.0f || defaultValue > 1.0f) {
        ParamInfo* paramInfo = FindParamInfo(index);
        if (paramInfo != nullptr)  {
          paramInfo->defaultFloatVal = defaultValue;
        }
      }
      SetParamRange(index, valueInput.MinValue, valueInput.MaxValue);
    }
  }
  virtual ~PluginImpl() {}

  FFResult InitGL(const FFGLViewportStruct* vp) override {
    if (!g_pipeline) {
      g_pipeline.reset(new Pipeline());
    }
    _program->EnsureResources();
    return CFFGLPlugin::InitGL(vp);
  }

  FFResult DeInitGL() override {
    return FF_SUCCESS;
  }

  void FinishDeclaration(int minInputs, int maxInputs) {
    SetMinInputs(1);
    SetMaxInputs(1);
  }

  FFResult ProcessOpenGL(ProcessOpenGLStruct* pGL) override {
    if (pGL->numInputTextures < 1) {
      return FF_FAIL;
    }

    if (pGL->inputTextures[0] == NULL) {
      return FF_FAIL;
    }

    // Blit OpenGL input texture over to Metal.
    const auto* pInputTexture = pGL->inputTextures[0];
    _inputTexture = ResizeTexture(_inputTexture, pInputTexture->HardwareWidth, pInputTexture->HardwareHeight, /* createGLFBO */ true);
    _outputTexture = ResizeTexture(_outputTexture, pInputTexture->HardwareWidth, pInputTexture->HardwareHeight);

    {
      auto& shader = g_pipeline->GetBlitFromTex2DShader();
      ScopedFBOBinding fboBinding(_inputTexture.openGLFBO, ScopedFBOBinding::RB_REVERT);
      ScopedShaderBinding shaderBinding(shader.GetGLID());
      ScopedSamplerActivation activateSampler(0);
      ScopedTextureBinding textureBinding(GL_TEXTURE_2D, pInputTexture->Handle);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MAG_FILTER, GL_NEAREST);

      shader.Set("InputTexture", 0);
      FFGLTexCoords maxCoords = GetMaxGLTexCoords(*pGL->inputTextures[0]);
      shader.Set("MaxUV", maxCoords.s, maxCoords.t);
      g_pipeline->GetFullViewportQuad().Draw();
    }

    glFlush();

    // Begin Metal rendering phase.
    _program->SetTextureInput(0, _inputTexture.metalTexture);
    _program->Run();
    _program->BlitOutputTexture(0, _outputTexture.metalTexture);

    // Blit result back to OpenGL output texture.
    {
      auto& shader = g_pipeline->GetBlitFromRectShader();
      ScopedShaderBinding shaderBinding(shader.GetGLID());
      ScopedSamplerActivation activateSampler(0);
      ScopedTextureBinding textureBinding(GL_TEXTURE_RECTANGLE, _outputTexture.openGLTexture);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
      glSamplerParameteri(GL_TEXTURE0, GL_TEXTURE_MAG_FILTER, GL_NEAREST);

      shader.Set("InputTexture", 0);
      FFGLTexCoords maxCoords = GetMaxGLTexCoordsRect(*pGL->inputTextures[0]);
      shader.Set("MaxUV", maxCoords.s, maxCoords.t);
      g_pipeline->GetFullViewportQuad().Draw();
    }
    return FF_SUCCESS;
  }

  virtual FFResult SetFloatParameter(uint index, float value) override {
    int valueInputIndex = index;
    if (valueInputIndex < 0 || valueInputIndex >= _program->GetValueInputCount()) {
      return FF_FAIL;
    }
    _program->SetValueInput(_parameterIndexMap[valueInputIndex], value);
    return FF_SUCCESS;
  }

  virtual float GetFloatParameter(uint index) override {
    int valueInputIndex = index;
    if (valueInputIndex < 0 || valueInputIndex >= _program->GetValueInputCount()) {
      return 0.0f;
    }
    return (float)_program->GetValueInput(_parameterIndexMap[valueInputIndex]);
  }

private:
  static AAPLOpenGLMetalInteropTexture* ResizeTexture(AAPLOpenGLMetalInteropTexture* texture, int width, int height, bool createGLFBO = false) {
    if (!texture || texture.width != width || texture.height != height) {
      texture = g_pipeline->CreateInteropTexture(width, height, createGLFBO);
      if (!texture) {
        FFGLLog::LogToHost("Failed to allocate texture.");
      }
    }
    return texture;
  }

  AAPLOpenGLMetalInteropTexture* _inputTexture = nullptr;
  AAPLOpenGLMetalInteropTexture* _outputTexture = nullptr;

  std::unique_ptr<NanoProgram> _program;
  std::vector<int> _parameterIndexMap;
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



