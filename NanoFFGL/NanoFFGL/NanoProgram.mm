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
    id<MTLBuffer> tmpSharedBuffer = [device newBufferWithBytes:_cpuBuffer.data() length:length options:MTLResourceStorageModeShared];

    id<MTLCommandBuffer> commandBuffer = NanoProgramGetCurrentCurrentMTLCommandBuffer();
    id<MTLBlitCommandEncoder> encoder = [commandBuffer blitCommandEncoder];
    [encoder copyFromBuffer:tmpSharedBuffer sourceOffset:0 toBuffer:_gpuBuffer destinationOffset:0 size:length];
    [encoder endEncoding];
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



class NanoProgram {
public:
  struct ParameterDecl {
    std::string Name;
    double DefaultValue;
    double MinValue;
    double MaxValue;
  };

  NanoProgram() {}
  virtual ~NanoProgram() {}
  
  MTLPixelFormat DefaultTextureFormat = MTLPixelFormatBGRA8Unorm_sRGB;

  void Run() {
    EnsureResources();
    SetCurrentInstance(this);
    _currentCommandBuffer = [_commandQueue commandBuffer];
    Execute();
    [_currentCommandBuffer commit];
    _currentCommandBuffer = nullptr;
    SetCurrentInstance(nullptr);
    ++_frameNumber;
  }

  void EnsureResources() {
    if (!_device) {
      _device = MTLCreateSystemDefaultDevice();
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

  virtual std::vector<ParameterDecl> GetParameterDecls() = 0;

  void SetupParameters() {
    std::vector<ParameterDecl> parameters = GetParameterDecls();
    _valueInputs.resize(parameters.size());
    for (int i = 0; i < parameters.size(); ++i) {
      _valueInputs[i] = parameters[i].DefaultValue;
    }
  }
  
  int GetValueInputCount() const { return (int)_valueInputs.size(); }

  double GetValueInput(int index) const {
    if (index < 0 || index >= _valueInputs.size()) {
      return 0.0;
    }
    return _valueInputs[index];
  }

  void SetValueInput(int index, double value) {
    if (index < 0 || index >= _valueInputs.size()) {
      return;
    }
    _valueInputs[index] = value;
  }

  // TODO: Should be const, but generator code doesn't yet support tagging functions with const.
  virtual int GetTextureInputCount() = 0;
  id<MTLTexture> GetTextureInput(int index) const {
    if (index < 0 || index >= _inputTextures.size()) {
      return nullptr;
    }
    return _inputTextures[index];
  }
  void SetTextureInput(int index, id<MTLTexture> value) {
    if (index < 0 || index >= _inputTextures.size()) {
      return;
    }
    _inputTextures[index] = value;
  }

  template<typename T> T SampleBuffer(const std::shared_ptr<NanoTypedBuffer<T>>& buffer, int index) { return (*buffer)[index]; }
  template<typename T> void WriteBuffer(const std::shared_ptr<NanoTypedBuffer<T>>& buffer, int index, T value) { (*buffer)[index] = value; }
  template<typename T> int GetLength(const std::shared_ptr<NanoTypedBuffer<T>>& buffer) { return buffer->GetElementCount(); }

  vector_int2 GetTextureSize(id<MTLTexture> texture) const { return vector_int2 { (int)texture.width, (int)texture.height }; }

  id<MTLDevice> GetDevice() const { return _device; }
  void SetDevice(id<MTLDevice> value) { _device = value; }
  id<MTLCommandQueue> GetCommandQueue() const { return _commandQueue; }
  void SetCommandQueue(id<MTLCommandQueue> value) { _commandQueue = value; }
  id<MTLCommandBuffer> GetCurrentCommandBuffer() const { return _currentCommandBuffer; }

  int GetFrameNumber() const { return _frameNumber; }

  static void SetCurrentInstance(NanoProgram* ptr) {
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
  static NanoProgram* GetCurrentInstance() {
    NSThread* thread = [NSThread currentThread];
    [_threadMapLock lock];
    if (!_threadMap) {
      return nullptr;
    }
    auto result = (*_threadMap)[thread];
    [_threadMapLock unlock];
    return result;
  }

protected:
  virtual void Execute() = 0;
  virtual void CreatePipelines() = 0;

private:
  id<MTLDevice> _device;
  id<MTLCommandQueue> _commandQueue;
  id<MTLCommandBuffer> _currentCommandBuffer;
  bool _createdPipelines = false;
  std::vector<double> _valueInputs;
  std::vector<id<MTLTexture>> _inputTextures;

  int _frameNumber = 0;

  static NSLock* _threadMapLock;
  static std::unique_ptr<std::map<NSThread*, NanoProgram*>> _threadMap;

protected:

  id<MTLTexture> ResizeTexture(id<MTLTexture> originalTexture, int width, int height) {
    if (originalTexture && originalTexture.width == width && originalTexture.height == height) {
      return originalTexture;
    }
    MTLTextureDescriptor* desc = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:DefaultTextureFormat width:width height:height mipmapped:NO];
    return [_device newTextureWithDescriptor:desc];
  }


  template<typename TFrom, typename TTo> static inline TTo Convert(const TFrom& value) {
    return value;
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

  static inline float max(float a, float b) { return std::max(a, b); }
  static inline vector_float2 max(vector_float2 a, vector_float2 b) { return vector_float2 { std::max(a.x, b.x), std::max(a.y, b.y) }; }
  static inline vector_float3 max(vector_float3 a, vector_float3 b) { return vector_float3 { std::max(a.x, b.x), std::max(a.y, b.y), std::max(a.z, b.z) }; }
  static inline vector_float4 max(vector_float4 a, vector_float4 b) { return vector_float4 { std::max(a.x, b.x), std::max(a.y, b.y), std::max(a.z, b.z), std::max(a.w, b.w) }; }

  static inline float min(float a, float b) { return std::min(a, b); }
  static inline vector_float2 min(vector_float2 a, vector_float2 b) { return vector_float2 { std::min(a.x, b.x), std::min(a.y, b.y) }; }
  static inline vector_float3 min(vector_float3 a, vector_float3 b) { return vector_float3 { std::min(a.x, b.x), std::min(a.y, b.y), std::min(a.z, b.z) }; }
  static inline vector_float4 min(vector_float4 a, vector_float4 b) { return vector_float4 { std::min(a.x, b.x), std::min(a.y, b.y), std::min(a.z, b.z), std::min(a.w, b.w) }; }

  static inline float pow(float a, float b) { return std::pow(a, b); }
  static inline vector_float2 pow(vector_float2 a, vector_float2 b) { return vector_float2 { std::pow(a.x, b.x), std::pow(a.y, b.y) }; }
  static inline vector_float3 pow(vector_float3 a, vector_float3 b) { return vector_float3 { std::pow(a.x, b.x), std::pow(a.y, b.y), std::pow(a.z, b.z) }; }
  static inline vector_float4 pow(vector_float4 a, vector_float4 b) { return vector_float4 { std::pow(a.x, b.x), std::pow(a.y, b.y), std::pow(a.z, b.z), std::pow(a.w, b.w) }; }

  static inline float log(float a, float b) { return std::log(a) / std::log(b); }
  static inline vector_float2 log(vector_float2 a, vector_float2 b) { return vector_float2 { log(a.x, b.x), log(a.y, b.y) }; }
  static inline vector_float3 log(vector_float3 a, vector_float3 b) { return vector_float3 { log(a.x, b.x), log(a.y, b.y), log(a.z, b.z) }; }
  static inline vector_float4 log(vector_float4 a, vector_float4 b) { return vector_float4 { log(a.x, b.x), log(a.y, b.y), log(a.z, b.z), log(a.w, b.w) }; }

  static inline float atan2(float a, float b) { return std::atan2(a, b); }
  static inline vector_float2 atan2(vector_float2 a, vector_float2 b) { return vector_float2 { std::atan2(a.x, b.x), std::atan2(a.y, b.y) }; }
  static inline vector_float3 atan2(vector_float3 a, vector_float3 b) { return vector_float3 { std::atan2(a.x, b.x), std::atan2(a.y, b.y), std::atan2(a.z, b.z) }; }
  static inline vector_float4 atan2(vector_float4 a, vector_float4 b) { return vector_float4 { std::atan2(a.x, b.x), std::atan2(a.y, b.y), std::atan2(a.z, b.z), std::atan2(a.w, b.w) }; }

  static inline float abs(float a) { return std::abs(a); }
  static inline vector_float2 abs(vector_float2 a) { return vector_float2 { std::abs(a.x), std::abs(a.y) }; }
  static inline vector_float3 abs(vector_float3 a) { return vector_float3 { std::abs(a.x), std::abs(a.y), std::abs(a.z) }; }
  static inline vector_float4 abs(vector_float4 a) { return vector_float4 { std::abs(a.x), std::abs(a.y), std::abs(a.z), std::abs(a.w) }; }

  static inline float clamp(float a) { return std::max(0.0f, std::min(1.0f, a)); }
  static inline vector_float2 clamp(vector_float2 a) { return vector_float2 { clamp(a.x), clamp(a.y) }; }
  static inline vector_float3 clamp(vector_float3 a) { return vector_float3 { clamp(a.x), clamp(a.y), clamp(a.z) }; }
  static inline vector_float4 clamp(vector_float4 a) { return vector_float4 { clamp(a.x), clamp(a.y), clamp(a.z), clamp(a.w) }; }

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

  static inline float sign(float a) { return a >= 0.0f ? 1.0f : -1.0f; }
  static inline vector_float2 sign(vector_float2 a) { return vector_float2 { sign(a.x), sign(a.y) }; }
  static inline vector_float3 sign(vector_float3 a) { return vector_float3 { sign(a.x), sign(a.y), sign(a.z) }; }
  static inline vector_float4 sign(vector_float4 a) { return vector_float4 { sign(a.x), sign(a.y), sign(a.z), sign(a.w) }; }

  static inline float cos(float a) { return std::cos(a); }
  static inline vector_float2 cos(vector_float2 a) { return vector_float2 { std::cos(a.x), std::cos(a.y) }; }
  static inline vector_float3 cos(vector_float3 a) { return vector_float3 { std::cos(a.x), std::cos(a.y), std::cos(a.z) }; }
  static inline vector_float4 cos(vector_float4 a) { return vector_float4 { std::cos(a.x), std::cos(a.y), std::cos(a.z), std::cos(a.w) }; }

  static inline float sin(float a) { return std::sin(a); }
  static inline vector_float2 sin(vector_float2 a) { return vector_float2 { std::sin(a.x), std::sin(a.y) }; }
  static inline vector_float3 sin(vector_float3 a) { return vector_float3 { std::sin(a.x), std::sin(a.y), std::sin(a.z) }; }
  static inline vector_float4 sin(vector_float4 a) { return vector_float4 { std::sin(a.x), std::sin(a.y), std::sin(a.z), std::sin(a.w) }; }

  static inline float exp(float a) { return std::exp(a); }
  static inline vector_float2 exp(vector_float2 a) { return vector_float2 { std::exp(a.x), std::exp(a.y) }; }
  static inline vector_float3 exp(vector_float3 a) { return vector_float3 { std::exp(a.x), std::exp(a.y), std::exp(a.z) }; }
  static inline vector_float4 exp(vector_float4 a) { return vector_float4 { std::exp(a.x), std::exp(a.y), std::exp(a.z), std::exp(a.w) }; }

  static inline float pow2(float a) { return std::pow(2.0f, a); }
  static inline vector_float2 pow2(vector_float2 a) { return vector_float2 { pow2(a.x), pow2(a.y) }; }
  static inline vector_float3 pow2(vector_float3 a) { return vector_float3 { pow2(a.x), pow2(a.y), pow2(a.z) }; }
  static inline vector_float4 pow2(vector_float4 a) { return vector_float4 { pow2(a.x), pow2(a.y), pow2(a.z), pow2(a.w) }; }

  static inline float pow10(float a) { return std::pow(10.0f, a); }
  static inline vector_float2 pow10(vector_float2 a) { return vector_float2 { pow10(a.x), pow10(a.y) }; }
  static inline vector_float3 pow10(vector_float3 a) { return vector_float3 { pow10(a.x), pow10(a.y), pow10(a.z) }; }
  static inline vector_float4 pow10(vector_float4 a) { return vector_float4 { pow10(a.x), pow10(a.y), pow10(a.z), pow10(a.w) }; }

  static inline float logE(float a) { return std::log(a); }
  static inline vector_float2 logE(vector_float2 a) { return vector_float2 { std::log(a.x), std::log(a.y) }; }
  static inline vector_float3 logE(vector_float3 a) { return vector_float3 { std::log(a.x), std::log(a.y), std::log(a.z) }; }
  static inline vector_float4 logE(vector_float4 a) { return vector_float4 { std::log(a.x), std::log(a.y), std::log(a.z), std::log(a.w) }; }

  static inline float log2(float a) { return std::log2(a); }
  static inline vector_float2 log2(vector_float2 a) { return vector_float2 { std::log2(a.x), std::log2(a.y) }; }
  static inline vector_float3 log2(vector_float3 a) { return vector_float3 { std::log2(a.x), std::log2(a.y), std::log2(a.z) }; }
  static inline vector_float4 log2(vector_float4 a) { return vector_float4 { std::log2(a.x), std::log2(a.y), std::log2(a.z), std::log2(a.w) }; }

  static inline float log10(float a) { return std::log10(a); }
  static inline vector_float2 log10(vector_float2 a) { return vector_float2 { std::log10(a.x), std::log10(a.y) }; }
  static inline vector_float3 log10(vector_float3 a) { return vector_float3 { std::log10(a.x), std::log10(a.y), std::log10(a.z) }; }
  static inline vector_float4 log10(vector_float4 a) { return vector_float4 { std::log10(a.x), std::log10(a.y), std::log10(a.z), std::log10(a.w) }; }

  static inline float atan(float a) { return std::atan(a); }
  static inline vector_float2 atan(vector_float2 a) { return vector_float2 { std::atan(a.x), std::atan(a.y) }; }
  static inline vector_float3 atan(vector_float3 a) { return vector_float3 { std::atan(a.x), std::atan(a.y), std::atan(a.z) }; }
  static inline vector_float4 atan(vector_float4 a) { return vector_float4 { std::atan(a.x), std::atan(a.y), std::atan(a.z), std::atan(a.w) }; }

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

  template<typename T> static T random_next();
  template<> float random_next<float>() { return std::rand() / (float) RAND_MAX; } // TODO: Re-evaluate random function.
  template<> vector_float2 random_next<vector_float2>() { return vector_float2 { random_next<float>(), random_next<float>() }; }
  template<> vector_float3 random_next<vector_float3>() { return vector_float3 { random_next<float>(), random_next<float>(), random_next<float>() }; }
  template<> vector_float4 random_next<vector_float4>() { return vector_float4 { random_next<float>(), random_next<float>(), random_next<float>(), random_next<float>() }; }
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





namespace {
  Program_Program* g_program = nullptr;
  
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
}


int main(int argc, const char* argv[]) {
  @autoreleasepool {
    g_program = new Program_Program();
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

    while (true) {
      std::string input;
      std::getline(std::cin, input);
//      std::cerr << "Happy days: " << input << "\n";
      std::string inputStr;
      macaron::Base64::Decode(input, inputStr);
//      std::cerr << "Happy byte days: " << inputStr << "\n";
      inputStr = input; // TODO: Remove. This is here for testing so we can type JSON directly.
      bool hadResponse = false;
      try {
        auto json = nlohmann::json::parse(input);
        std::cerr << json.dump() << "\n";
        if (json.contains(kGetDefinitionKey)) {
          nlohmann::json request = json[kGetDefinitionKey].get<nlohmann::json>();
          std::cerr << "Request: " << request << "\n";

          nlohmann::json response = {
            { "Name", "<placeholder>" },
            { "TextureInputCount", g_program->GetTextureInputCount() },
          };

          std::cerr << "Response: " << response << "\n";
          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kGetParametersRequestKey)) {
          nlohmann::json request = json[kGetParametersRequestKey].get<nlohmann::json>();
          std::cerr << "Request: " << request << "\n";

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

          std::cerr << "Response: " << response << "\n";
          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kSetParametersRequestKey)) {
          nlohmann::json request = json[kSetParametersRequestKey].get<nlohmann::json>();
          std::cerr << "Request: " << request << "\n";
          
          nlohmann::json parameters = request["Values"].get<nlohmann::json>();
          for (const auto& [key, value] : parameters.items()) {
            auto findIt = parameterMap.find(key);
            if (findIt == parameterMap.end()) {
              continue;
            }
            g_program->SetValueInput(findIt->second, value.get<double>());
          }

          nlohmann::json response;
          std::cerr << "Response: " << response << "\n";
          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        } else if (json.contains(kProcessTexturesRequestKey)) {
          nlohmann::json request = json[kProcessTexturesRequestKey].get<nlohmann::json>();
          std::cerr << "Request: " << request << "\n";
          
          std::vector<id<MTLTexture>> inputTextures;
          std::vector<id<MTLTexture>> outputTextures;
          std::vector<IOSurfaceRef> acquiredSurfaces;
          id<MTLDevice> device = g_program->GetDevice();
          if (inputTextures.size() == 0)
          for (const auto& value : request["TextureInputs"].get<nlohmann::json>()) {
            int32_t ioSurfaceId = value.get<int32_t>();
            IOSurfaceRef surface = IOSurfaceLookup(ioSurfaceId);
            id<MTLTexture> texture = IOSurfaceToTexture(device, surface);
            inputTextures.push_back(texture);
            acquiredSurfaces.push_back(surface);
          }
          if (outputTextures.size() == 0)
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

          g_program->Run();

          if (outputTextures.size() > 0) {
            id<MTLTexture> textureOutput = g_program->GetOutput0();
            id<MTLCommandBuffer> commandBuffer = g_program->GetCommandQueue().commandBuffer;
            id<MTLBlitCommandEncoder> encoder = [commandBuffer blitCommandEncoder];
            [encoder copyFromTexture:textureOutput
                         sourceSlice:0
                         sourceLevel:0
                        sourceOrigin:MTLOriginMake(0, 0, 0)
                          sourceSize:MTLSizeMake(textureOutput.width, textureOutput.height, 0)
                           toTexture:outputTextures[0]
                    destinationSlice:0
                    destinationLevel:0
                   destinationOrigin:MTLOriginMake(0, 0, 0)];
            [encoder endEncoding];
            [commandBuffer commit];
            std::cerr << "Output (" << textureOutput.width << ", " << textureOutput.height << ")\n";
            std::cerr << "Blitted to output (" << outputTextures[0].width << ", " << outputTextures[0].height << ")\n";
          }
          for (IOSurfaceRef surface : acquiredSurfaces) {
            CFRelease(surface);
          }

          nlohmann::json response;
          std::cout << EncodeResponse(response) << "\n";
          hadResponse = true;
        }
      } catch(nlohmann::json::exception e) {
        std::cerr << "JSON error.\n";
      }
//      std::cerr << "GetOutput0: " << g_program->GetOutput0() << "\n";
      if (!hadResponse) {
        nlohmann::json response;
        std::cout << EncodeResponse(response) << "\n";
      }
    }
  }
  return 1;
}










