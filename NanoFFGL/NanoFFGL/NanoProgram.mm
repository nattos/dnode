#include "Prefix.pch"

#include "PonkSender.h"

namespace {
  id<MTLDevice> NanoProgramGetCurrentMTLDevice();
  id<MTLCommandQueue> NanoProgramGetCurrentMTLCommandQueue();
  id<MTLCommandBuffer> NanoProgramGetCurrentCurrentMTLCommandBuffer();
}

#define NanoTexture id<MTLTexture>

class NanoBuffer {
public:
  NanoBuffer(int elementSize)
      : ElementSize(elementSize) {
  }

  int GetByteLength() const { return ElementSize; }
  int GetTotalByteLength() const { return ElementSize * _elementCount; }
  int GetElementCount() const { return _elementCount; }
  void* GetCpuBuffer() { return _cpuBuffer.data(); }
  id<MTLBuffer> GetGpuBuffer() const { return _gpuBuffer; }

  void Resize(int elementCount) {
    if (elementCount == _elementCount) {
      if (_elementCount == 0) {
        _hasCpuBuffer = true;
        _cpuBuffer.reserve(ElementSize);
      }
      return;
    }
    _elementCount = elementCount;
    _hasCpuBuffer = true;
    _cpuBuffer.resize(_elementCount * ElementSize);
    if (_elementCount == 0) {
      _cpuBuffer.reserve(ElementSize);
    }
    if (_gpuBuffer && _gpuBufferCapacityCount < _elementCount) {
      // TODO: Retain GPU buffer data.
      // TODO: Resize by larger steps.
      _gpuBufferCapacityCount = _elementCount;
      _gpuBuffer = nullptr;
      EnsureGpuBuffer();
    } else {
      _gpuBufferDirty = true;
    }
  }
  void EnsureCapacity(int elementCount) {
    if (elementCount <= _elementCount) {
      if (_elementCount == 0) {
        _cpuBuffer.reserve(ElementSize);
      }
      return;
    }
    _cpuBuffer.reserve(std::max(1, _elementCount) * ElementSize);
    EnsureGpuBufferCapacity();
  }

  void EnsureCpuBuffer() {
    // TODO: Implement.
    if (_hasCpuBuffer) {
      return;
    }
    _hasCpuBuffer = true;
    _cpuBuffer.resize(_elementCount * ElementSize);
    _cpuBuffer.reserve(ElementSize);
    _cpuBufferDirty = true;
    _gpuBufferDirty = true;
  }
  void EnsureGpuBuffer() {
    // TODO: Implement.
    if (_gpuBuffer) {
      return;
    }
    _cpuBufferDirty = true;
    _gpuBufferDirty = true;
    id<MTLDevice> device = NanoProgramGetCurrentMTLDevice();
    _gpuBuffer = [device newBufferWithLength:(_elementCount * ElementSize) options:MTLResourceStorageModePrivate];
  }

  void MarkCpuBufferChanged() { _cpuBufferDirty = false; _gpuBufferDirty = true; }
  void MarkGpuBufferChanged() { _gpuBufferDirty = false; _cpuBufferDirty = true; }

  void SyncToCpu();
  void SyncToGpu() {
    if (!_gpuBufferDirty) {
      return;
    }
    EnsureGpuBuffer();
    _cpuBufferDirty = false;
    _gpuBufferDirty = false;
    if (!_hasCpuBuffer) {
      return;
    }
    id<MTLDevice> device = NanoProgramGetCurrentMTLDevice();
    int length = _elementCount * ElementSize;
    if (length > 0) {
      id<MTLBuffer> tmpSharedBuffer = [device newBufferWithBytes:_cpuBuffer.data() length:length options:MTLResourceStorageModeShared];

      id<MTLCommandBuffer> commandBuffer = NanoProgramGetCurrentCurrentMTLCommandBuffer();
      id<MTLBlitCommandEncoder> encoder = [commandBuffer blitCommandEncoder];
      [encoder copyFromBuffer:tmpSharedBuffer sourceOffset:0 toBuffer:_gpuBuffer destinationOffset:0 size:length];
      [encoder endEncoding];
    }
  }

  void CopyCpuFrom(NanoBuffer* other) {
    assert(this);
    assert(other);
    swap(this->_cpuBuffer, other->_cpuBuffer);
    this->_hasCpuBuffer = true;
    this->_elementCount = other->_elementCount;
    EnsureGpuBufferCapacity();
    MarkCpuBufferChanged();
  }

  const int ElementSize;

private:
  void EnsureGpuBufferCapacity() {
    if (_gpuBuffer && _gpuBufferCapacityCount < _elementCount) {
      // TODO: Retain GPU buffer data.
      // TODO: Resize by larger steps.
      _gpuBufferCapacityCount = _elementCount;
      _gpuBuffer = nullptr;
      EnsureGpuBuffer();
    }
  }

  int _elementCount;
  bool _hasCpuBuffer = false;
  std::vector<uint8> _cpuBuffer;
  int _gpuBufferCapacityCount = 0;
  id<MTLBuffer> _gpuBuffer;

  bool _cpuBufferDirty;
  bool _gpuBufferDirty;
};

typedef std::shared_ptr<NanoBuffer> NanoBufferPtr;

template<typename T>
class NanoTypedBuffer : public NanoBuffer {
public:
  NanoTypedBuffer() : NanoBuffer(sizeof(T)) {}

  static NanoTypedBuffer<T>* Allocate(int length) {
    NanoTypedBuffer<T>* buffer = new NanoTypedBuffer<T>();
    buffer->Resize(length);
    buffer->EnsureCpuBuffer();
    return buffer;
  }

  T& operator[](int index) { return ((T*)GetCpuBuffer())[index % GetElementCount()]; }
  const T& operator[](int index) const { return ((T*)GetCpuBuffer())[index % GetElementCount()]; }
};




class NanoSharedTexture {
 public:
  id<MTLTexture> Texture;
  IOSurfaceRef IOSurface;
  int32_t IOSurfaceId;

  ~NanoSharedTexture() {
    if (IOSurface) {
      CFRelease(IOSurface);
    }
  }
  
  static NanoSharedTexture* Create(id<MTLDevice> device, int32_t width, int32_t height) {
    NSDictionary *attribs = @{ (NSString *)kIOSurfaceIsGlobal: @YES,
                               (NSString *)kIOSurfaceWidth: @(width),
                               (NSString *)kIOSurfaceHeight: @(height),
                               (NSString *)kIOSurfaceBytesPerElement: @4u };
    IOSurfaceRef ioSurface = IOSurfaceCreate((CFDictionaryRef)attribs);

    MTLTextureDescriptor *desc = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm_sRGB
                                                                                    width:width
                                                                                   height:height
                                                                                mipmapped:NO];
    id<MTLTexture> texture = [device newTextureWithDescriptor:desc iosurface:ioSurface plane:0];
    int32_t ioSurfaceId = IOSurfaceGetID(ioSurface);
    return new NanoSharedTexture { texture, ioSurface, ioSurfaceId };
  }
};



NanoProgram::NanoProgram() {}
NanoProgram::~NanoProgram() {}

void NanoProgram::Run() {
  EnsureResources();
  double nextFrameTime = [NSDate now].timeIntervalSince1970 - _startTime;
  _frameDeltaTime = nextFrameTime - _frameTime;
  _frameTime = nextFrameTime;

  SetCurrentInstance(this);
  _currentCommandBuffer = [_commandQueue commandBuffer];
  Execute();
  [_currentCommandBuffer commit];
  _currentCommandBuffer = nullptr;
  SetCurrentInstance(nullptr);
  ++_frameNumber;
}

void NanoProgram::SetupParameters() {
  std::vector<ParameterDecl> parameters = GetParameterDecls();
  _valueInputs.resize(parameters.size());
  for (int i = 0; i < parameters.size(); ++i) {
    _valueInputs[i] = parameters[i].DefaultValue;
  }
}

int NanoProgram::GetValueInputCount() const { return (int)_valueInputs.size(); }

double NanoProgram::GetValueInput(int index) const {
  if (index < 0 || index >= _valueInputs.size()) {
    return 0.0;
  }
  return _valueInputs[index];
}

void NanoProgram::SetValueInput(int index, double value) {
  if (index < 0 || index >= _valueInputs.size()) {
    return;
  }
  _valueInputs[index] = value;
}

id<MTLTexture> NanoProgram::GetTextureInput(int index) const {
  if (index < 0 || index >= _inputTextures.size()) {
    return nullptr;
  }
  return _inputTextures[index];
}
void NanoProgram::SetTextureInput(int index, id<MTLTexture> value) {
  if (index < 0 || index >= _inputTextures.size()) {
    return;
  }
  _inputTextures[index] = value;
}

int32_t NanoProgram::DebugGetOutputTextureSurfaceId() const { return _debugOutputTexture ? _debugOutputTexture->IOSurfaceId : 0; }

void NanoProgram::SetCurrentInstance(NanoProgram* ptr) {
  NSThread* thread = [NSThread currentThread];
  [_threadMapLock lock];
  if (!_threadMap) {
    _threadMap.reset(new std::map<NSThread*, NanoProgram*>());
  }
  if (!ptr) {
    _threadMap->erase(thread);
  } else {
    (*_threadMap)[thread] = ptr;
  }
  [_threadMapLock unlock];
}
NanoProgram* NanoProgram::GetCurrentInstance() {
  NSThread* thread = [NSThread currentThread];
  [_threadMapLock lock];
  if (!_threadMap) {
    return nullptr;
  }
  auto result = (*_threadMap)[thread];
  [_threadMapLock unlock];
  return result;
}

void NanoProgram::DoPonkOutput(id<MTLBuffer> counterBuffer, id<MTLBuffer> pathPointsBuffer, id<MTLBuffer> pathIndexBuffer) {
  if (!_ponkSender) {
    _ponkSender.reset(new PONKSender());
  }

  std::vector<std::vector<PonkSenderPoint>> lines;
  if (_ponkSyncToCpuCommandBuffer && _ponkCounterBuffer && _ponkPathPointBuffer && _ponkPathIndexBuffer) {
    [_ponkSyncToCpuCommandBuffer waitUntilCompleted];
    _ponkSyncToCpuCommandBuffer = nullptr;
    int count = ((const int *)_ponkCounterBuffer.contents)[0];
    const vector_float2* points = (const vector_float2*)_ponkPathPointBuffer.contents;
    const int* indices = (const int*)_ponkPathIndexBuffer.contents;
    for (int i = 0; i < count; ++i) {
      const vector_float2 point = points[i];
      const int index = indices[i];
      if (index < 0 || index >= PonkMaxPaths) {
        continue;
      }
      while (lines.size() <= index) {
        lines.emplace_back();
      }
      vector_float2 clientPoint = point;
      std::vector<PonkSenderPoint>& polyline = lines[index];
      PonkSenderPoint ponkSenderPoint = {
        .Point = vector_float2 { clientPoint.x, clientPoint.y },
        .Color = vector_float4(1.0f),
      };
      polyline.push_back(ponkSenderPoint);
    }
  }
  for (int i = lines.size() - 1; i >= 0; --i) {
    if (lines[i].size() == 0) {
      lines.erase(lines.begin() + i);
    }
  }
  _ponkSender->draw(lines);

  if (!counterBuffer || !pathPointsBuffer || !pathIndexBuffer) {
    return;
  }
  _ponkCounterBuffer = [_device newBufferWithLength:counterBuffer.length options:MTLResourceStorageModeManaged];
  _ponkPathPointBuffer = [_device newBufferWithLength:pathPointsBuffer.length options:MTLResourceStorageModeManaged];
  _ponkPathIndexBuffer = [_device newBufferWithLength:pathIndexBuffer.length options:MTLResourceStorageModeManaged];
  {
    id<MTLCommandQueue> commandQueue = GetCommandQueue();
    id<MTLCommandBuffer> buffer = [commandQueue commandBuffer];
    id<MTLBlitCommandEncoder> encoder = [buffer blitCommandEncoder];
    [encoder copyFromBuffer:counterBuffer sourceOffset:0 toBuffer:_ponkCounterBuffer destinationOffset:0 size:counterBuffer.length];
    [encoder copyFromBuffer:pathPointsBuffer sourceOffset:0 toBuffer:_ponkPathPointBuffer destinationOffset:0 size:pathPointsBuffer.length];
    [encoder copyFromBuffer:pathIndexBuffer sourceOffset:0 toBuffer:_ponkPathIndexBuffer destinationOffset:0 size:pathIndexBuffer.length];
    [encoder synchronizeResource:_ponkCounterBuffer];
    [encoder synchronizeResource:_ponkPathPointBuffer];
    [encoder synchronizeResource:_ponkPathIndexBuffer];
    [encoder endEncoding];
    // TODO: Use [buffer addCompletedHandler:] to reduce latency.
    [buffer commit];
    _ponkSyncToCpuCommandBuffer = buffer;
  }
}









  template<typename T> T SampleBuffer(const std::shared_ptr<NanoTypedBuffer<T>>& buffer, int index) { return (*buffer)[index]; }
  template<typename T> void WriteBuffer(const std::shared_ptr<NanoTypedBuffer<T>>& buffer, int index, T value) { (*buffer)[index] = value; }
  template<typename T> int GetLength(const std::shared_ptr<NanoTypedBuffer<T>>& buffer) { return buffer->GetElementCount(); }
  template<typename T> int GetDebugLength(const std::shared_ptr<NanoTypedBuffer<T>>& buffer) { return !buffer ? 0 : buffer->GetElementCount(); }


  id<MTLTexture> NanoProgram::ResizeTexture(id<MTLTexture> originalTexture, int width, int height, MTLPixelFormat format) {
    width = std::max(1, width);
    height = std::max(1, height);
    if (originalTexture && originalTexture.width == width && originalTexture.height == height) {
      return originalTexture;
    }
    MTLTextureDescriptor* desc = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:format width:width height:height mipmapped:NO];
    desc.usage = MTLTextureUsageShaderRead | MTLTextureUsageShaderWrite | MTLTextureUsageRenderTarget;
    
    return [_device newTextureWithDescriptor:desc];
  }
  
  void NanoProgram::ResizeSharedTexture(std::unique_ptr<NanoSharedTexture>& ptr, id<MTLDevice> device, int32_t width, int32_t height) {
    width = std::max(1, width);
    height = std::max(1, height);
    if (ptr && ptr->Texture.width == width && ptr->Texture.height == height) {
      return;
    }
    ptr.reset(NanoSharedTexture::Create(device, width, height));
  }


  template<typename TFrom, typename TTo> static inline TTo Convert(TFrom value) {
    return value;
  }

  template<> inline vector_float2 Convert<vector_int2, vector_float2>(vector_int2 value) {
    return vector_float2 { Convert<int, float>(value.x), Convert<int, float>(value.y) };
  }
  template<> inline vector_float3 Convert<vector_int3, vector_float3>(vector_int3 value) {
    return vector_float3 { Convert<int, float>(value.x), Convert<int, float>(value.y), Convert<int, float>(value.z) };
  }
  template<> inline vector_float4 Convert<vector_int4, vector_float4>(vector_int4 value) {
    return vector_float4 { Convert<int, float>(value.x), Convert<int, float>(value.y), Convert<int, float>(value.z), Convert<int, float>(value.w) };
  }

  template<> inline vector_int2 Convert<vector_float2, vector_int2>(vector_float2 value) {
    return vector_int2 { Convert<float, int>(value.x), Convert<float, int>(value.y) };
  }
  template<> inline vector_int3 Convert<vector_float3, vector_int3>(vector_float3 value) {
    return vector_int3 { Convert<float, int>(value.x), Convert<float, int>(value.y), Convert<float, int>(value.z) };
  }
  template<> inline vector_int4 Convert<vector_float4, vector_int4>(vector_float4 value) {
    return vector_int4 { Convert<float, int>(value.x), Convert<float, int>(value.y), Convert<float, int>(value.z), Convert<float, int>(value.w) };
  }

  template<> inline vector_double4 Convert<vector_float4, vector_double4>(vector_float4 value) {
    return vector_double4 { Convert<float, double>(value.x), Convert<float, double>(value.y), Convert<float, double>(value.z), Convert<float, double>(value.w) };
  }

  template<> vector_float2 inline Convert<float, vector_float2>(float value) {
    return vector_float2 { value, value };
  }
  template<> vector_float3 inline Convert<float, vector_float3>(float value) {
    return vector_float3 { value, value, value };
  }
  template<> vector_float4 inline Convert<float, vector_float4>(float value) {
    return vector_float4 { value, value, value, value };
  }

  template<> float inline Convert<vector_float2, float>(vector_float2 value) {
    return value.x;
  }
  template<> vector_float3 inline Convert<vector_float2, vector_float3>(vector_float2 value) {
    return vector_float3 { value.x, value.y, 0.0f };
  }
  template<> vector_float4 inline Convert<vector_float2, vector_float4>(vector_float2 value) {
    return vector_float4 { value.x, value.y, 0.0f, 0.0f };
  }

  template<> float inline Convert<vector_float3, float>(vector_float3 value) {
    return value.x;
  }
  template<> vector_float2 inline Convert<vector_float3, vector_float2>(vector_float3 value) {
    return vector_float2 { value.x, value.y };
  }
  template<> vector_float4 inline Convert<vector_float3, vector_float4>(vector_float3 value) {
    return vector_float4 { value.x, value.y, value.z, 0.0f };
  }

  template<> float inline Convert<vector_float4, float>(vector_float4 value) {
    return value.x;
  }
  template<> vector_float2 inline Convert<vector_float4, vector_float2>(vector_float4 value) {
    return vector_float2 { value.x, value.y };
  }
  template<> vector_float3 inline Convert<vector_float4, vector_float3>(vector_float4 value) {
    return vector_float3 { value.x, value.y, value.z };
  }



  template<typename TFrom, typename TTo> static inline std::shared_ptr<NanoTypedBuffer<TTo>> ConvertArray(const std::shared_ptr<NanoTypedBuffer<TFrom>>& value) {
    int32_t elementCount = (int32_t)(value.get())->GetElementCount();
    std::shared_ptr<NanoTypedBuffer<TTo>> result(NanoTypedBuffer<TTo>::Allocate(elementCount));
    for (int i = 0; i < elementCount; ++i) {
      (*result)[i] = Convert<TFrom, TTo>((*value)[i]);
    }
    return result;
  }

  template<typename TFrom, typename TTo> static inline std::shared_ptr<NanoTypedBuffer<TTo>> ConvertArray(const TFrom& value) {
    std::shared_ptr<NanoTypedBuffer<TTo>> result(NanoTypedBuffer<TTo>::Allocate(1));
    (*result)[0] = Convert<TFrom, TTo>(value);
    return result;
  }
  
  template<typename T> static inline void CopyCpuFrom(std::shared_ptr<NanoTypedBuffer<T>>& to, const std::shared_ptr<NanoTypedBuffer<T>>& from) {
    if (!to) {
      to.reset(new NanoTypedBuffer<T>());
    }
    to->CopyCpuFrom(from.get());
  }

  template<typename T> inline T copy_value(T value) { return value; }

  static inline float modulo_op(int a, int b) { return (a % b); }
  static inline float modulo_op(float a, float b) { return std::fmodf(a, b); }
  static inline vector_float2 modulo_op(vector_float2 a, vector_float2 b) { return vector_float2 { modulo_op(a.x, b.x), modulo_op(a.y, b.y) }; }
  static inline vector_float3 modulo_op(vector_float3 a, vector_float3 b) { return vector_float3 { modulo_op(a.x, b.x), modulo_op(a.y, b.y), modulo_op(a.z, b.z) }; }
  static inline vector_float4 modulo_op(vector_float4 a, vector_float4 b) { return vector_float4 { modulo_op(a.x, b.x), modulo_op(a.y, b.y), modulo_op(a.z, b.z), modulo_op(a.w, b.w) }; }

  static inline float max_op(float a) { return a; }
  static inline float max_op(float a, float b) { return std::max(a, b); }
  static inline vector_float2 max_op(vector_float2 a, vector_float2 b) { return vector_float2 { std::max(a.x, b.x), std::max(a.y, b.y) }; }
  static inline vector_float3 max_op(vector_float3 a, vector_float3 b) { return vector_float3 { std::max(a.x, b.x), std::max(a.y, b.y), std::max(a.z, b.z) }; }
  static inline vector_float4 max_op(vector_float4 a, vector_float4 b) { return vector_float4 { std::max(a.x, b.x), std::max(a.y, b.y), std::max(a.z, b.z), std::max(a.w, b.w) }; }

  static inline float min_op(float a) { return a; }
  static inline float min_op(float a, float b) { return std::min(a, b); }
  static inline vector_float2 min_op(vector_float2 a, vector_float2 b) { return vector_float2 { std::min(a.x, b.x), std::min(a.y, b.y) }; }
  static inline vector_float3 min_op(vector_float3 a, vector_float3 b) { return vector_float3 { std::min(a.x, b.x), std::min(a.y, b.y), std::min(a.z, b.z) }; }
  static inline vector_float4 min_op(vector_float4 a, vector_float4 b) { return vector_float4 { std::min(a.x, b.x), std::min(a.y, b.y), std::min(a.z, b.z), std::min(a.w, b.w) }; }

  static inline float lerp_op(float a, float b, float t) { return a * (1.0f - t) + b * t; }
  static inline vector_float2 lerp_op(vector_float2 a, vector_float2 b, float t) { return vector_float2 { lerp_op(a.x, b.x, t), lerp_op(a.y, b.y, t) }; }
  static inline vector_float3 lerp_op(vector_float3 a, vector_float3 b, float t) { return vector_float3 { lerp_op(a.x, b.x, t), lerp_op(a.y, b.y, t), lerp_op(a.z, b.z, t) }; }
  static inline vector_float4 lerp_op(vector_float4 a, vector_float4 b, float t) { return vector_float4 { lerp_op(a.x, b.x, t), lerp_op(a.y, b.y, t), lerp_op(a.z, b.z, t), lerp_op(a.w, b.w, t) }; }

  static inline float pow_op(float a, float b) { return std::pow(a, b); }
  static inline vector_float2 pow_op(vector_float2 a, vector_float2 b) { return vector_float2 { std::pow(a.x, b.x), std::pow(a.y, b.y) }; }
  static inline vector_float3 pow_op(vector_float3 a, vector_float3 b) { return vector_float3 { std::pow(a.x, b.x), std::pow(a.y, b.y), std::pow(a.z, b.z) }; }
  static inline vector_float4 pow_op(vector_float4 a, vector_float4 b) { return vector_float4 { std::pow(a.x, b.x), std::pow(a.y, b.y), std::pow(a.z, b.z), std::pow(a.w, b.w) }; }

  static inline float log_op(float a, float b) { return std::log(a) / std::log(b); }
  static inline vector_float2 log_op(vector_float2 a, vector_float2 b) { return vector_float2 { log_op(a.x, b.x), log_op(a.y, b.y) }; }
  static inline vector_float3 log_op(vector_float3 a, vector_float3 b) { return vector_float3 { log_op(a.x, b.x), log_op(a.y, b.y), log_op(a.z, b.z) }; }
  static inline vector_float4 log_op(vector_float4 a, vector_float4 b) { return vector_float4 { log_op(a.x, b.x), log_op(a.y, b.y), log_op(a.z, b.z), log_op(a.w, b.w) }; }

  static inline float atan_op(float a) { return std::atan(a); }
  static inline vector_float2 atan_op(vector_float2 a) { return vector_float2 { atan_op(a.x), atan_op(a.y) }; }
  static inline vector_float3 atan_op(vector_float3 a) { return vector_float3 { atan_op(a.x), atan_op(a.y), atan_op(a.z) }; }
  static inline vector_float4 atan_op(vector_float4 a) { return vector_float4 { atan_op(a.x), atan_op(a.y), atan_op(a.z), atan_op(a.w) }; }

  static inline float atan2_op(float a, float b) { return std::atan2(a, b); }
  static inline vector_float2 atan2_op(vector_float2 a, vector_float2 b) { return vector_float2 { std::atan2(a.x, b.x), std::atan2(a.y, b.y) }; }
  static inline vector_float3 atan2_op(vector_float3 a, vector_float3 b) { return vector_float3 { std::atan2(a.x, b.x), std::atan2(a.y, b.y), std::atan2(a.z, b.z) }; }
  static inline vector_float4 atan2_op(vector_float4 a, vector_float4 b) { return vector_float4 { std::atan2(a.x, b.x), std::atan2(a.y, b.y), std::atan2(a.z, b.z), std::atan2(a.w, b.w) }; }

  static inline float abs_op(float a) { return std::abs(a); }
  static inline vector_float2 abs_op(vector_float2 a) { return vector_float2 { std::abs(a.x), std::abs(a.y) }; }
  static inline vector_float3 abs_op(vector_float3 a) { return vector_float3 { std::abs(a.x), std::abs(a.y), std::abs(a.z) }; }
  static inline vector_float4 abs_op(vector_float4 a) { return vector_float4 { std::abs(a.x), std::abs(a.y), std::abs(a.z), std::abs(a.w) }; }

  static inline float saturate(float a) { return std::max(0.0f, std::min(1.0f, a)); }
  static inline vector_float2 saturate(vector_float2 a) { return vector_float2 { saturate(a.x), saturate(a.y) }; }
  static inline vector_float3 saturate(vector_float3 a) { return vector_float3 { saturate(a.x), saturate(a.y), saturate(a.z) }; }
  static inline vector_float4 saturate(vector_float4 a) { return vector_float4 { saturate(a.x), saturate(a.y), saturate(a.z), saturate(a.w) }; }

  static inline float negate(float a) { return -a; }
  static inline vector_float2 negate(vector_float2 a) { return vector_float2 { negate(a.x), negate(a.y) }; }
  static inline vector_float3 negate(vector_float3 a) { return vector_float3 { negate(a.x), negate(a.y), negate(a.z) }; }
  static inline vector_float4 negate(vector_float4 a) { return vector_float4 { negate(a.x), negate(a.y), negate(a.z), negate(a.w) }; }

  static inline float invert(float a) { return -a; }
  static inline vector_float2 invert(vector_float2 a) { return vector_float2 { invert(a.x), invert(a.y) }; }
  static inline vector_float3 invert(vector_float3 a) { return vector_float3 { invert(a.x), invert(a.y), invert(a.z) }; }
  static inline vector_float4 invert(vector_float4 a) { return vector_float4 { invert(a.x), invert(a.y), invert(a.z), invert(a.w) }; }

  static inline float one_minus(float a) { return 1.0f - a; }
  static inline vector_float2 one_minus(vector_float2 a) { return vector_float2 { one_minus(a.x), one_minus(a.y) }; }
  static inline vector_float3 one_minus(vector_float3 a) { return vector_float3 { one_minus(a.x), one_minus(a.y), one_minus(a.z) }; }
  static inline vector_float4 one_minus(vector_float4 a) { return vector_float4 { one_minus(a.x), one_minus(a.y), one_minus(a.z), one_minus(a.w) }; }

  static inline float sign_op(float a) { return a >= 0.0f ? 1.0f : -1.0f; }
  static inline vector_float2 sign_op(vector_float2 a) { return vector_float2 { sign_op(a.x), sign_op(a.y) }; }
  static inline vector_float3 sign_op(vector_float3 a) { return vector_float3 { sign_op(a.x), sign_op(a.y), sign_op(a.z) }; }
  static inline vector_float4 sign_op(vector_float4 a) { return vector_float4 { sign_op(a.x), sign_op(a.y), sign_op(a.z), sign_op(a.w) }; }

  static inline float floor_op(float a) { return std::floor(a); }
  static inline vector_float2 floor_op(vector_float2 a) { return vector_float2 { floor_op(a.x), floor_op(a.y) }; }
  static inline vector_float3 floor_op(vector_float3 a) { return vector_float3 { floor_op(a.x), floor_op(a.y), floor_op(a.z) }; }
  static inline vector_float4 floor_op(vector_float4 a) { return vector_float4 { floor_op(a.x), floor_op(a.y), floor_op(a.z), floor_op(a.w) }; }

  static inline float ceil_op(float a) { return std::ceil(a); }
  static inline vector_float2 ceil_op(vector_float2 a) { return vector_float2 { ceil_op(a.x), ceil_op(a.y) }; }
  static inline vector_float3 ceil_op(vector_float3 a) { return vector_float3 { ceil_op(a.x), ceil_op(a.y), ceil_op(a.z) }; }
  static inline vector_float4 ceil_op(vector_float4 a) { return vector_float4 { ceil_op(a.x), ceil_op(a.y), ceil_op(a.z), ceil_op(a.w) }; }

  static inline float round_op(float a) { return std::round(a); }
  static inline vector_float2 round_op(vector_float2 a) { return vector_float2 { round_op(a.x), round_op(a.y) }; }
  static inline vector_float3 round_op(vector_float3 a) { return vector_float3 { round_op(a.x), round_op(a.y), round_op(a.z) }; }
  static inline vector_float4 round_op(vector_float4 a) { return vector_float4 { round_op(a.x), round_op(a.y), round_op(a.z), round_op(a.w) }; }

  static inline float cos_op(float a) { return std::cos(a); }
  static inline vector_float2 cos_op(vector_float2 a) { return vector_float2 { std::cos(a.x), std::cos(a.y) }; }
  static inline vector_float3 cos_op(vector_float3 a) { return vector_float3 { std::cos(a.x), std::cos(a.y), std::cos(a.z) }; }
  static inline vector_float4 cos_op(vector_float4 a) { return vector_float4 { std::cos(a.x), std::cos(a.y), std::cos(a.z), std::cos(a.w) }; }

  static inline float sin_op(float a) { return std::sin(a); }
  static inline vector_float2 sin_op(vector_float2 a) { return vector_float2 { std::sin(a.x), std::sin(a.y) }; }
  static inline vector_float3 sin_op(vector_float3 a) { return vector_float3 { std::sin(a.x), std::sin(a.y), std::sin(a.z) }; }
  static inline vector_float4 sin_op(vector_float4 a) { return vector_float4 { std::sin(a.x), std::sin(a.y), std::sin(a.z), std::sin(a.w) }; }

  static inline float exp_op(float a) { return std::exp(a); }
  static inline vector_float2 exp_op(vector_float2 a) { return vector_float2 { std::exp(a.x), std::exp(a.y) }; }
  static inline vector_float3 exp_op(vector_float3 a) { return vector_float3 { std::exp(a.x), std::exp(a.y), std::exp(a.z) }; }
  static inline vector_float4 exp_op(vector_float4 a) { return vector_float4 { std::exp(a.x), std::exp(a.y), std::exp(a.z), std::exp(a.w) }; }

  static inline float pow2_op(float a) { return std::pow(2.0f, a); }
  static inline vector_float2 pow2_op(vector_float2 a) { return vector_float2 { pow2_op(a.x), pow2_op(a.y) }; }
  static inline vector_float3 pow2_op(vector_float3 a) { return vector_float3 { pow2_op(a.x), pow2_op(a.y), pow2_op(a.z) }; }
  static inline vector_float4 pow2_op(vector_float4 a) { return vector_float4 { pow2_op(a.x), pow2_op(a.y), pow2_op(a.z), pow2_op(a.w) }; }

  static inline float pow10_op(float a) { return std::pow(10.0f, a); }
  static inline vector_float2 pow10_op(vector_float2 a) { return vector_float2 { pow10_op(a.x), pow10_op(a.y) }; }
  static inline vector_float3 pow10_op(vector_float3 a) { return vector_float3 { pow10_op(a.x), pow10_op(a.y), pow10_op(a.z) }; }
  static inline vector_float4 pow10_op(vector_float4 a) { return vector_float4 { pow10_op(a.x), pow10_op(a.y), pow10_op(a.z), pow10_op(a.w) }; }

  static inline float logE_op(float a) { return std::log(a); }
  static inline vector_float2 logE_op(vector_float2 a) { return vector_float2 { std::log(a.x), std::log(a.y) }; }
  static inline vector_float3 logE_op(vector_float3 a) { return vector_float3 { std::log(a.x), std::log(a.y), std::log(a.z) }; }
  static inline vector_float4 logE_op(vector_float4 a) { return vector_float4 { std::log(a.x), std::log(a.y), std::log(a.z), std::log(a.w) }; }

  static inline float log2_op(float a) { return std::log2(a); }
  static inline vector_float2 log2_op(vector_float2 a) { return vector_float2 { std::log2(a.x), std::log2(a.y) }; }
  static inline vector_float3 log2_op(vector_float3 a) { return vector_float3 { std::log2(a.x), std::log2(a.y), std::log2(a.z) }; }
  static inline vector_float4 log2_op(vector_float4 a) { return vector_float4 { std::log2(a.x), std::log2(a.y), std::log2(a.z), std::log2(a.w) }; }

  static inline float log10_op(float a) { return std::log10(a); }
  static inline vector_float2 log10_op(vector_float2 a) { return vector_float2 { std::log10(a.x), std::log10(a.y) }; }
  static inline vector_float3 log10_op(vector_float3 a) { return vector_float3 { std::log10(a.x), std::log10(a.y), std::log10(a.z) }; }
  static inline vector_float4 log10_op(vector_float4 a) { return vector_float4 { std::log10(a.x), std::log10(a.y), std::log10(a.z), std::log10(a.w) }; }

  static inline float magnitude_op(float a) { return a; }
  static inline vector_float2 magnitude_op(vector_float2 a) { return std::sqrt(a.x * a.x + a.y * a.y); }
  static inline vector_float3 magnitude_op(vector_float3 a) { return std::sqrt(a.x * a.x + a.y * a.y + a.z * a.z); }
  static inline vector_float4 magnitude_op(vector_float4 a) { return std::sqrt(a.x * a.x + a.y * a.y + a.z * a.z + a.w * a.w); }
  static inline float normalize_op(float a) { return sign_op(a); }
  static inline vector_float2 normalize_op(vector_float2 a) { return a / magnitude_op(a); }
  static inline vector_float3 normalize_op(vector_float3 a) { return a / magnitude_op(a); }
  static inline vector_float4 normalize_op(vector_float4 a) { return a / magnitude_op(a); }

  template<typename T>
  struct ValueAndBool {
    T Value;
    bool Flag;
  };
  
  template<typename T> static inline bool to_bool(const T& value) { return Convert<T, bool>(value); }
  template<typename T> static inline bool to_bool(const ValueAndBool<T>& value) { return value.Flag; }

  template<typename T> static inline bool not_op(const T& value) { return !to_bool(value); }
  template<typename T> static inline bool and_op(const T& lhs, const T& rhs) { return to_bool(lhs) && to_bool(rhs); }
  template<typename T> static inline bool or_op(const T& lhs, const T& rhs) { return to_bool(lhs) || to_bool(rhs); }
  template<typename T> static inline bool xor_op(const T& lhs, const T& rhs) { return to_bool(lhs) ^ to_bool(rhs); }
  template<typename T> static inline ValueAndBool<T> greater_than_op(const T& lhs, const T& rhs) { return ValueAndBool<T> { rhs, (lhs > rhs) }; }
  template<typename T> static inline ValueAndBool<T> less_than_op(const T& lhs, const T& rhs) { return ValueAndBool<T> { rhs, (lhs < rhs) }; }
  template<typename T> static inline ValueAndBool<T> greater_or_equal_op(const T& lhs, const T& rhs) { return ValueAndBool<T> { rhs, (lhs >= rhs) }; }
  template<typename T> static inline ValueAndBool<T> less_or_equal_op(const T& lhs, const T& rhs) { return ValueAndBool<T> { rhs, (lhs <= rhs) }; }

  template<bool useAlpha, typename T> static inline T lerp_mix(T rhs, float t) { return rhs * t; }
  template<bool useAlpha, typename T> static inline T lerp_mix(T rhs, T lhs, float t) { return lerp_op(rhs, lhs, t); }
  
  template<bool useAlpha, typename T> static inline T blend_mix(T rhs, float t) { return rhs * t; }
  template<bool useAlpha, typename T> static inline T blend_mix(T rhs, T lhs, float t) { return lerp_op(rhs, lhs, t); }
  template<> inline vector_float4 blend_mix<true, vector_float4>(vector_float4 rhs, vector_float4 lhs, float t) {
    if (t < 0.5f) {
      float a = lhs.a * t * 2.0f;
      vector_float3 rgb = lerp_op(vector_float3 { rhs.x, rhs.y, rhs.z }, vector_float3 { lhs.x, lhs.y, lhs.z }, a);
      float aOut = lerp_op(rhs.a, 1.0f, a);
      return vector_float4 { rgb.x, rgb.y, rgb.z, aOut };
    } else {
      float a = t * 2.0f - 1.0f;
      vector_float3 rgb = lerp_op(vector_float3 { rhs.x, rhs.y, rhs.z } * (1.0f - a), vector_float3 { lhs.x, lhs.y, lhs.z }, lhs.a);
      float aOut = lerp_op(lerp_op(rhs.a, 1.0f, lhs.a), lhs.a, a);
      return vector_float4 { rgb.x, rgb.y, rgb.z, aOut };
    }
  }

  template<typename T> static T random_next();
  template<> float random_next<float>() { return std::rand() / (float) RAND_MAX; } // TODO: Re-evaluate random function.
  template<> double random_next<double>() { return std::rand() / (double) RAND_MAX; } // TODO: Re-evaluate random function.
  template<> int32_t random_next<int32_t>() { return std::rand(); } // TODO: Re-evaluate random function.
  template<> vector_float2 random_next<vector_float2>() { return vector_float2 { random_next<float>(), random_next<float>() }; }
  template<> vector_float3 random_next<vector_float3>() { return vector_float3 { random_next<float>(), random_next<float>(), random_next<float>() }; }
  template<> vector_float4 random_next<vector_float4>() { return vector_float4 { random_next<float>(), random_next<float>(), random_next<float>(), random_next<float>() }; }

  template<typename T> T ArraySum(const std::shared_ptr<NanoTypedBuffer<T>>& buffer) {
    T acc = 0;
    int length = GetLength(buffer);
    for (int i = 0; i < length; ++i) {
      acc += SampleBuffer(buffer, i);
    }
    return acc;
  }

  vector_int2 GetTextureSize(id<MTLTexture> texture) { return vector_int2 { (int)texture.width, (int)texture.height }; }
  vector_float2 GetTextureSizeFloat(id<MTLTexture> texture) { return Convert<vector_int2, vector_float2>(GetTextureSize(texture)); }



template<typename T> static void BlitOutputTextureImpl(T input, id<MTLTexture> outputTexture, NanoProgram* program, BlitOutputTextureResources* resources) {
  id<MTLCommandBuffer> commandBuffer = program->GetCommandQueue().commandBuffer;
  id<MTLComputeCommandEncoder> encoder = [commandBuffer computeCommandEncoder];
  [encoder setComputePipelineState:resources->ClearTextureSolidPipeline];
  vector_float4 color = Convert<T, vector_float4>(input);
  [encoder setBytes:&color length:sizeof(color) atIndex:0];
  [encoder setTexture:outputTexture atIndex:1];
  MTLSize batchSize = { outputTexture.width, outputTexture.height, 1 };
  MTLSize threadgroupSize = { resources->ClearTextureSolidPipeline.maxTotalThreadsPerThreadgroup, 1, 1 };
  [encoder dispatchThreads:batchSize threadsPerThreadgroup:threadgroupSize];
  [encoder endEncoding];
  [commandBuffer commit];
}
template<> void BlitOutputTextureImpl<id<MTLTexture>>(id<MTLTexture> textureOutput, id<MTLTexture> outputTexture, NanoProgram* program, BlitOutputTextureResources* resources) {
  id<MTLCommandBuffer> commandBuffer = program->GetCommandQueue().commandBuffer;
  if (textureOutput.width == outputTexture.width && textureOutput.height == outputTexture.height) {
    if (textureOutput.pixelFormat == outputTexture.pixelFormat) {
      id<MTLBlitCommandEncoder> encoder = [commandBuffer blitCommandEncoder];
      [encoder copyFromTexture:textureOutput
                   sourceSlice:0
                   sourceLevel:0
                  sourceOrigin:MTLOriginMake(0, 0, 0)
                    sourceSize:MTLSizeMake(textureOutput.width, textureOutput.height, 0)
                     toTexture:outputTexture
              destinationSlice:0
              destinationLevel:0
             destinationOrigin:MTLOriginMake(0, 0, 0)];
      [encoder endEncoding];
    } else {
      id<MTLComputeCommandEncoder> encoder = [commandBuffer computeCommandEncoder];
      [encoder setComputePipelineState:resources->CopyTextureSampleNearestPipeline];
      [encoder setTexture:textureOutput atIndex:0];
      [encoder setTexture:outputTexture atIndex:1];
      MTLSize batchSize = { outputTexture.width, outputTexture.height, 1 };
      MTLSize threadgroupSize = { resources->CopyTextureSampleNearestPipeline.maxTotalThreadsPerThreadgroup, 1, 1 };
      [encoder dispatchThreads:batchSize threadsPerThreadgroup:threadgroupSize];
      [encoder endEncoding];
    }
  } else {
    id<MTLComputeCommandEncoder> encoder = [commandBuffer computeCommandEncoder];
    [encoder setComputePipelineState:resources->CopyTextureSampleLinearPipeline];
    [encoder setTexture:textureOutput atIndex:0];
    [encoder setTexture:outputTexture atIndex:1];
    MTLSize batchSize = { outputTexture.width, outputTexture.height, 1 };
    MTLSize threadgroupSize = { resources->CopyTextureSampleLinearPipeline.maxTotalThreadsPerThreadgroup, 1, 1 };
    [encoder dispatchThreads:batchSize threadsPerThreadgroup:threadgroupSize];
    [encoder endEncoding];
  }
  [commandBuffer commit];
};







NSLock* NanoProgram::_threadMapLock = [[NSLock alloc] init];
std::unique_ptr<std::map<NSThread*, NanoProgram*>> NanoProgram::_threadMap;



namespace {
  id<MTLDevice> NanoProgramGetCurrentMTLDevice() {
    return NanoProgram::GetCurrentInstance()->GetDevice();
  }

  id<MTLCommandQueue> NanoProgramGetCurrentMTLCommandQueue() {
    return NanoProgram::GetCurrentInstance()->GetCommandQueue();
  }

  id<MTLCommandBuffer> NanoProgramGetCurrentCurrentMTLCommandBuffer() {
    return NanoProgram::GetCurrentInstance()->GetCurrentCommandBuffer();
  }
}



#include "Generated/Program.incl.h"



NanoProgram* CreateProgram() {
  return new NanoProgramImpl();
}


void NanoProgram::EnsureResources() {
  if (!_device) {
    _device = MTLCreateSystemDefaultDevice();
    _startTime = [NSDate now].timeIntervalSince1970;

    NSError* error;
    id<MTLLibrary> defaultLibrary = [_device newDefaultLibraryWithBundle:[NSBundle bundleForClass:[NanoProgramPlaceholder class]] error:&error];
    id<MTLComputePipelineState> copyTextureSampleNearestPipeline = [_device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@"CopyTextureSampleNearest"] error:&error];
    id<MTLComputePipelineState> copyTextureSampleLinearPipeline = [_device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@"CopyTextureSampleLinear"] error:&error];
    id<MTLComputePipelineState> clearTextureSolidPipeline = [_device newComputePipelineStateWithFunction:[defaultLibrary newFunctionWithName:@"ClearTextureSolid"] error:&error];
    _blitOutputTextureResources = {
      .CopyTextureSampleNearestPipeline = copyTextureSampleNearestPipeline,
      .CopyTextureSampleLinearPipeline = copyTextureSampleLinearPipeline,
      .ClearTextureSolidPipeline = clearTextureSolidPipeline,
    };
  }
  if (!_commandQueue) {
    _commandQueue = [_device newCommandQueue];
  }
  if (!_createdPipelines) {
    _createdPipelines = true;
    CreatePipelines();
  }
  _inputTextures.resize(GetTextureInputCount());
}

void NanoProgram::BlitOutputTexture(int index, id<MTLTexture> outputTexture) {
  BlitOutputTextureImpl(((NanoProgramImpl*)this)->GetOutput0(), outputTexture, this, &this->_blitOutputTextureResources);
}










