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

vector_float2 Convert_float_To_vector_float2(float value) {
  return vector_float2 { value, value };
}

vector_float3 Convert_float_To_vector_float3(float value) {
  return vector_float3 { value, value, value };
}

vector_float4 Convert_float_To_vector_float4(float value) {
  return vector_float4 { value, value, value, value };
}

vector_float3 Convert_vector_float2_To_vector_float3(vector_float2 value) {
  return vector_float3 { value.x, value.y, 0.0f };
}

vector_float4 Convert_vector_float3_To_vector_float4(vector_float3 value) {
  return vector_float4 { value.x, value.y, value.z, 0.0f };
}

vector_float2 Convert_vector_float3_To_vector_float2(vector_float3 value) {
  return vector_float2 { value.x, value.y };
}

vector_float2 Convert_vector_float4_To_vector_float2(vector_float4 value) {
  return vector_float2 { value.x, value.y };
}

float Convert_vector_float4_To_float(vector_float4 value) {
  return value.x;
}

#include "/Users/nattos/Desktop/Program.metal.incl.h"

