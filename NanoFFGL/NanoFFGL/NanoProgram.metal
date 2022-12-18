#include <metal_stdlib>

using namespace metal;

#define NanoTexture texture2d<half, access::sample>
#define NanoWriteTexture texture2d<half, access::write>

template<typename T> T SampleBuffer(device const T* buffer, int index) {
  return buffer[index];
}

template<typename T> void WriteBuffer(device T* buffer, int index, T value) {
  buffer[index] = value;
}

float4 SampleTexture(NanoTexture texture, float2 uv) {
  constexpr sampler linearSampler(coord::normalized, address::clamp_to_edge, filter::linear);
  return float4(texture.sample(linearSampler, uv));
}

void WriteTexture(NanoWriteTexture texture, vector_uint2 gid_xy, float4 value) {
  texture.write(half4(value), gid_xy);
}

template<typename T> inline T copy_value(T value) { return value; }

template<typename TFrom, typename TTo> inline TTo Convert(TFrom value) {
  return value;
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

template<> vector_float3 inline Convert<vector_float2, vector_float3>(vector_float2 value) {
  return vector_float3 { value.x, value.y, 0.0f };
}

template<> vector_float4 inline Convert<vector_float3, vector_float4>(vector_float3 value) {
  return vector_float4 { value.x, value.y, value.z, 0.0f };
}

template<> vector_float2 inline Convert<vector_float3, vector_float2>(vector_float3 value) {
  return vector_float2 { value.x, value.y };
}

template<> vector_float2 inline Convert<vector_float4, vector_float2>(vector_float4 value) {
  return vector_float2 { value.x, value.y };
}

template<> float inline Convert<vector_float4, float>(vector_float4 value) {
  return value.x;
}

#include "Generated/Program.metal.incl.h"

