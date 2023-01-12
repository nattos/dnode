#import "Foundation/Foundation.h"
#import "simd/simd.h"
#import "MetalKit/MetalKit.h"
#import "CoreVideo/CoreVideo.h"

#include <iostream>
#include <map>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

#include "Base64.h"
#include "json.hpp"

#include "NanoProgram.h"

namespace {
  NanoProgram* g_program = nullptr;
  
  id<MTLTexture> IOSurfaceToTexture(id<MTLDevice> device, IOSurfaceRef surface) {
    if (!surface) {
      return nullptr;
    }
    MTLTextureDescriptor* desc =
        [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm_sRGB
                                                           width:IOSurfaceGetWidth(surface)
                                                          height:IOSurfaceGetHeight(surface)
                                                       mipmapped:NO];
    id<MTLTexture> texture = [device newTextureWithDescriptor:desc iosurface:surface plane:0];
    return texture;
  }
  
  std::string EncodeResponse(const nlohmann::json& json) {
    // TODO: Encode base64.
    return json.dump();
  }

  constexpr const char* kGetDefinitionKey = "GetDefinition";
  constexpr const char* kGetParametersRequestKey = "GetParameters";
  constexpr const char* kSetParametersRequestKey = "SetParameters";
  constexpr const char* kProcessTexturesRequestKey = "ProcessTextures";
  constexpr const char* kDebugGetWatchedValuesRequestKey = "DebugGetWatchedValues";
  constexpr const char* kDebugSetValuesRequestKey = "DebugSetValues";
}


int main(int argc, const char* argv[]) {
  @autoreleasepool {
    g_program = CreateProgram();
    g_program->EnsureResources();
    g_program->SetupParameters();

    std::unordered_map<std::string, int> parameterMap;
    {
      int parameterIndex = 0;
      for (const auto& parameterDecl : g_program->GetParameterDecls()) {
        parameterMap[parameterDecl.Name] = parameterIndex;
        ++parameterIndex;
      }
    }
    std::unordered_map<std::string, NanoProgram::DebugSettableValue> debugValuesMap;
    {
      std::vector<NanoProgram::DebugSettableValue> debugValues = g_program->GetDebugSettableValues();
      for (const auto& entry : debugValues) {
        debugValuesMap[entry.Key] = entry;
      }
    }

    while (true) {
    @autoreleasepool {
      std::string input;
      std::getline(std::cin, input);
      if (input.length() == 0) {
        continue;
      }
      std::string inputStr;
//      macaron::Base64::Decode(input, inputStr);
      inputStr = input; // TODO: Remove. This is here for testing so we can type JSON directly.
      bool hadResponse = false;
      try {
        auto json = nlohmann::json::parse(input);
        if (json.contains(kGetDefinitionKey)) {
          nlohmann::json request = json[kGetDefinitionKey].get<nlohmann::json>();

          nlohmann::json response = {
            { "Name", "<placeholder>" },
            { "TextureInputCount", g_program->GetTextureInputCount() },
          };

          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kGetParametersRequestKey)) {
          nlohmann::json request = json[kGetParametersRequestKey].get<nlohmann::json>();

          nlohmann::json response;
          nlohmann::json parameters;
          int parameterIndex = 0;
          for (const auto& parameterDecl : g_program->GetParameterDecls()) {
            double currentValue = g_program->GetValueInput(parameterIndex);
            nlohmann::json parameter = {
              { "Name", parameterDecl.Name },
              { "Value", currentValue },
              { "DefaultValue", parameterDecl.DefaultValue },
              { "MinValue", parameterDecl.MinValue },
              { "MaxValue", parameterDecl.MaxValue },
            };
            parameters.push_back(parameter);
            ++parameterIndex;
          }
          response["Parameters"] = parameters;

          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kSetParametersRequestKey)) {
          nlohmann::json request = json[kSetParametersRequestKey].get<nlohmann::json>();
          
          const nlohmann::json& parameters = request["Values"].get<nlohmann::json>();
          for (const auto& [key, value] : parameters.items()) {
            auto findIt = parameterMap.find(key);
            if (findIt == parameterMap.end()) {
              continue;
            }
            g_program->SetValueInput(findIt->second, value.get<double>());
          }

          nlohmann::json response;
          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kProcessTexturesRequestKey)) {
          double processTextureStartTime = [NSDate now].timeIntervalSince1970;
          nlohmann::json request = json[kProcessTexturesRequestKey].get<nlohmann::json>();
          
          std::vector<id<MTLTexture>> inputTextures;
          std::vector<id<MTLTexture>> outputTextures;
          std::vector<IOSurfaceRef> acquiredSurfaces;
          id<MTLDevice> device = g_program->GetDevice();
          for (const auto& value : request["TextureInputs"].get<nlohmann::json>()) {
            int32_t ioSurfaceId = value.get<int32_t>();
            IOSurfaceRef surface = IOSurfaceLookup(ioSurfaceId);
            id<MTLTexture> texture = IOSurfaceToTexture(device, surface);
            inputTextures.push_back(texture);
            acquiredSurfaces.push_back(surface);
          }
          for (const auto& value : request["TextureOutputs"].get<nlohmann::json>()) {
            int32_t ioSurfaceId = value.get<int32_t>();
            IOSurfaceRef surface = IOSurfaceLookup(ioSurfaceId);
            id<MTLTexture> texture = IOSurfaceToTexture(device, surface);
            outputTextures.push_back(texture);
            acquiredSurfaces.push_back(surface);
          }

          int textureCount = g_program->GetTextureInputCount();
          for (int i = 0; i < textureCount; ++i) {
            id<MTLTexture> texture = nullptr;
            if (i < inputTextures.size()) {
              texture = inputTextures[i];
            }
            g_program->SetTextureInput(i, texture);
          }
          std::string debugOutputTextureKey = request.value<std::string>("DebugOutputTextureKey", "");
          g_program->DebugSetOutputTextureKey(debugOutputTextureKey);

          g_program->Run();

          if (outputTextures.size() > 0) {
            id<MTLTexture> outputTexture = outputTextures[0];
            g_program->BlitOutputTexture(0, outputTexture);
          }
          for (IOSurfaceRef surface : acquiredSurfaces) {
            CFRelease(surface);
          }

          double processTextureEndTime = [NSDate now].timeIntervalSince1970;
          double processTextureTime = processTextureEndTime - processTextureStartTime;

          nlohmann::json response;
          response["DebugOutputTexture"] = g_program->DebugGetOutputTextureSurfaceId();
          response["DebugFrameTime"] = processTextureTime;
          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kDebugGetWatchedValuesRequestKey)) {
          nlohmann::json request = json[kDebugGetWatchedValuesRequestKey].get<nlohmann::json>();

          nlohmann::json response;
          nlohmann::json values;
          for (const auto& value : g_program->GetDebugValues()) {
            nlohmann::json currentValue;
            currentValue.push_back(value.Value.x);
            if (value.Length >=2) {
              currentValue.push_back(value.Value.y);
            }
            if (value.Length >= 3) {
              currentValue.push_back(value.Value.z);
            }
            if (value.Length >= 4) {
              currentValue.push_back(value.Value.w);
            }
            nlohmann::json valueJson = {
              { "Key", value.Key },
              { "Values", currentValue },
            };
            values.push_back(valueJson);
          }
          response["Values"] = values;

          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kDebugSetValuesRequestKey)) {
          nlohmann::json request = json[kDebugSetValuesRequestKey].get<nlohmann::json>();

          const nlohmann::json& debugValues = request["Values"].get<nlohmann::json>();
          std::vector<double> valuesArray;
          for (const auto& value : debugValues) {
            std::string key = value["Key"].get<std::string>();
            auto findIt = debugValuesMap.find(key);
            if (findIt == debugValuesMap.end()) {
              continue;
            }
            const nlohmann::json valuesJson = value["Values"].get<nlohmann::json>();
            for (const auto& entry : valuesJson) {
              valuesArray.push_back(entry.get<double>());
            }
            findIt->second.Setter(valuesArray);
            valuesArray.clear();
          }

          nlohmann::json response;
          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        }
      } catch(nlohmann::json::exception e) {
        std::cerr << "JSON error.\n";
      }
      if (!hadResponse) {
        nlohmann::json response;
        std::cout << EncodeResponse(response) << "\n";
      }
      std::flush(std::cout);
    } }
  }
  return 1;
}



