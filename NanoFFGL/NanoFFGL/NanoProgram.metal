#include <metal_stdlib>
#include <metal_atomic>

using namespace metal;

#define NanoTexture texture2d<half, access::sample>
#define NanoWriteTexture texture2d<half, access::write>

template<typename T> T SampleBuffer(device const T* buffer, int index) {
  return buffer[index];
}

template<typename T> void WriteBuffer(device T* buffer, int index, T value) {
  buffer[index] = value;
}

template<filter FilterMode, address AddressMode> float4 SampleTexture(NanoTexture texture, float2 uv) {
  constexpr sampler linearSampler(coord::normalized, FilterMode, AddressMode);
  return float4(texture.sample(linearSampler, uv));
}

void WriteTexture(NanoWriteTexture texture, vector_uint2 gid_xy, float4 value) {
  texture.write(half4(value), gid_xy);
}

float2 GetTextureSizeFloat(NanoTexture texture) {
  return float2(texture.get_width(), texture.get_height());
}

template<typename T> inline T copy_value(T value) { return value; }

template<typename TFrom, typename TTo> inline TTo Convert(TFrom value) {
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



template<typename T> static inline float min_op(T a, T b) { return min(a, b); }
template<typename T> static inline float max_op(T a, T b) { return max(a, b); }


static inline float atan_op(float a) { return atan(a); }
static inline vector_float2 atan_op(vector_float2 a) { return vector_float2 { atan_op(a.x), atan_op(a.y) }; }
static inline vector_float3 atan_op(vector_float3 a) { return vector_float3 { atan_op(a.x), atan_op(a.y), atan_op(a.z) }; }
static inline vector_float4 atan_op(vector_float4 a) { return vector_float4 { atan_op(a.x), atan_op(a.y), atan_op(a.z), atan_op(a.w) }; }

static inline float atan2_op(float a, float b) { return atan2(a, b); }
static inline vector_float2 atan2_op(vector_float2 a, vector_float2 b) { return vector_float2 { atan2_op(a.x, b.x), atan2_op(a.y, b.y) }; }
static inline vector_float3 atan2_op(vector_float3 a, vector_float3 b) { return vector_float3 { atan2_op(a.x, b.x), atan2_op(a.y, b.y), atan2_op(a.z, b.z) }; }
static inline vector_float4 atan2_op(vector_float4 a, vector_float4 b) { return vector_float4 { atan2_op(a.x, b.x), atan2_op(a.y, b.y), atan2_op(a.z, b.z), atan2_op(a.w, b.w) }; }

static inline float abs_op(float a) { return abs(a); }
static inline vector_float2 abs_op(vector_float2 a) { return vector_float2 { abs(a.x), abs(a.y) }; }
static inline vector_float3 abs_op(vector_float3 a) { return vector_float3 { abs(a.x), abs(a.y), abs(a.z) }; }
static inline vector_float4 abs_op(vector_float4 a) { return vector_float4 { abs(a.x), abs(a.y), abs(a.z), abs(a.w) }; }

static inline float modulo_op(int a, int b) { return (a % b); }
static inline float modulo_op(float a, float b) { return modf(a, b); }
static inline vector_float2 modulo_op(vector_float2 a, vector_float2 b) { return vector_float2 { modulo_op(a.x, b.x), modulo_op(a.y, b.y) }; }
static inline vector_float3 modulo_op(vector_float3 a, vector_float3 b) { return vector_float3 { modulo_op(a.x, b.x), modulo_op(a.y, b.y), modulo_op(a.z, b.z) }; }
static inline vector_float4 modulo_op(vector_float4 a, vector_float4 b) { return vector_float4 { modulo_op(a.x, b.x), modulo_op(a.y, b.y), modulo_op(a.z, b.z), modulo_op(a.w, b.w) }; }

static inline float lerp_op(float a, float b, float t) { return mix(a, b, t); }
static inline vector_float2 lerp_op(vector_float2 a, vector_float2 b, float t) { return mix(a, b, t); }
static inline vector_float3 lerp_op(vector_float3 a, vector_float3 b, float t) { return mix(a, b, t); }
static inline vector_float4 lerp_op(vector_float4 a, vector_float4 b, float t) { return mix(a, b, t); }

static inline float log(float a, float b) { return log(a) / log(b); }
static inline vector_float2 log(vector_float2 a, vector_float2 b) { return vector_float2 { log(a.x, b.x), log(a.y, b.y) }; }
static inline vector_float3 log(vector_float3 a, vector_float3 b) { return vector_float3 { log(a.x, b.x), log(a.y, b.y), log(a.z, b.z) }; }
static inline vector_float4 log(vector_float4 a, vector_float4 b) { return vector_float4 { log(a.x, b.x), log(a.y, b.y), log(a.z, b.z), log(a.w, b.w) }; }

static inline float negate(float a) { return -a; }
static inline vector_float2 negate(vector_float2 a) { return vector_float2 { negate(a.x), negate(a.y) }; }
static inline vector_float3 negate(vector_float3 a) { return vector_float3 { negate(a.x), negate(a.y), negate(a.z) }; }
static inline vector_float4 negate(vector_float4 a) { return vector_float4 { negate(a.x), negate(a.y), negate(a.z), negate(a.w) }; }

static inline float invert(float a) { return 1.0 / a; }
static inline vector_float2 invert(vector_float2 a) { return vector_float2 { invert(a.x), invert(a.y) }; }
static inline vector_float3 invert(vector_float3 a) { return vector_float3 { invert(a.x), invert(a.y), invert(a.z) }; }
static inline vector_float4 invert(vector_float4 a) { return vector_float4 { invert(a.x), invert(a.y), invert(a.z), invert(a.w) }; }

static inline float one_minus(float a) { return 1.0f - a; }
static inline vector_float2 one_minus(vector_float2 a) { return vector_float2 { one_minus(a.x), one_minus(a.y) }; }
static inline vector_float3 one_minus(vector_float3 a) { return vector_float3 { one_minus(a.x), one_minus(a.y), one_minus(a.z) }; }
static inline vector_float4 one_minus(vector_float4 a) { return vector_float4 { one_minus(a.x), one_minus(a.y), one_minus(a.z), one_minus(a.w) }; }

static inline float floor_op(float a) { return floor(a); }
static inline vector_float2 floor_op(vector_float2 a) { return vector_float2 { floor_op(a.x), floor_op(a.y) }; }
static inline vector_float3 floor_op(vector_float3 a) { return vector_float3 { floor_op(a.x), floor_op(a.y), floor_op(a.z) }; }
static inline vector_float4 floor_op(vector_float4 a) { return vector_float4 { floor_op(a.x), floor_op(a.y), floor_op(a.z), floor_op(a.w) }; }

static inline float ceil_op(float a) { return ceil(a); }
static inline vector_float2 ceil_op(vector_float2 a) { return vector_float2 { ceil_op(a.x), ceil_op(a.y) }; }
static inline vector_float3 ceil_op(vector_float3 a) { return vector_float3 { ceil_op(a.x), ceil_op(a.y), ceil_op(a.z) }; }
static inline vector_float4 ceil_op(vector_float4 a) { return vector_float4 { ceil_op(a.x), ceil_op(a.y), ceil_op(a.z), ceil_op(a.w) }; }

static inline float round_op(float a) { return round(a); }
static inline vector_float2 round_op(vector_float2 a) { return vector_float2 { round_op(a.x), round_op(a.y) }; }
static inline vector_float3 round_op(vector_float3 a) { return vector_float3 { round_op(a.x), round_op(a.y), round_op(a.z) }; }
static inline vector_float4 round_op(vector_float4 a) { return vector_float4 { round_op(a.x), round_op(a.y), round_op(a.z), round_op(a.w) }; }

template<typename T> static inline float magnitude_op(T value) { return length(value); }
template<typename T> static inline T normalize_op(T value) { return normalize(value); }

static inline float pow2(float a) { return pow(2.0f, a); }
static inline vector_float2 pow2(vector_float2 a) { return vector_float2 { pow2(a.x), pow2(a.y) }; }
static inline vector_float3 pow2(vector_float3 a) { return vector_float3 { pow2(a.x), pow2(a.y), pow2(a.z) }; }
static inline vector_float4 pow2(vector_float4 a) { return vector_float4 { pow2(a.x), pow2(a.y), pow2(a.z), pow2(a.w) }; }

static inline float pow10(float a) { return pow(10.0f, a); }
static inline vector_float2 pow10(vector_float2 a) { return vector_float2 { pow10(a.x), pow10(a.y) }; }
static inline vector_float3 pow10(vector_float3 a) { return vector_float3 { pow10(a.x), pow10(a.y), pow10(a.z) }; }
static inline vector_float4 pow10(vector_float4 a) { return vector_float4 { pow10(a.x), pow10(a.y), pow10(a.z), pow10(a.w) }; }

static inline float logE(float a) { return log(a); }
static inline vector_float2 logE(vector_float2 a) { return vector_float2 { log(a.x), log(a.y) }; }
static inline vector_float3 logE(vector_float3 a) { return vector_float3 { log(a.x), log(a.y), log(a.z) }; }
static inline vector_float4 logE(vector_float4 a) { return vector_float4 { log(a.x), log(a.y), log(a.z), log(a.w) }; }

template<typename T> static inline T truncate_op(const T value) { return trunc(value); }
template<typename T> static inline T fraction_op(const T value) { return fract(value); }
template<typename T> static inline T pow_op(const T a, const T b) { return pow(a, b); }
template<typename T> static inline T cos_op(const T value) { return cos(value); }
template<typename T> static inline T sin_op(const T value) { return sin(value); }

template<typename T>
struct ValueAndBool {
  T Value;
  bool Flag;
};

template<typename T> static inline bool to_bool(const T value) { return Convert<T, bool>(value); }
template<typename T> static inline bool to_bool(const ValueAndBool<T> value) { return value.Flag; }

template<typename T> static inline bool not_op(const T value) { return !to_bool(value); }
template<typename T> static inline bool and_op(const T lhs, const T rhs) { return to_bool(lhs) && to_bool(rhs); }
template<typename T> static inline bool or_op(const T lhs, const T rhs) { return to_bool(lhs) || to_bool(rhs); }
template<typename T> static inline bool xor_op(const T lhs, const T rhs) { return to_bool(lhs) ^ to_bool(rhs); }
template<typename T> static inline ValueAndBool<T> greater_than_op(const T lhs, const T rhs) { return ValueAndBool<T> { rhs, (lhs > rhs) }; }
template<typename T> static inline ValueAndBool<T> less_than_op(const T lhs, const T rhs) { return ValueAndBool<T> { rhs, (lhs < rhs) }; }
template<typename T> static inline ValueAndBool<T> greater_or_equal_op(const T lhs, const T rhs) { return ValueAndBool<T> { rhs, (lhs >= rhs) }; }
template<typename T> static inline ValueAndBool<T> less_or_equal_op(const T lhs, const T rhs) { return ValueAndBool<T> { rhs, (lhs <= rhs) }; }

template<bool useAlpha, typename T> static inline T lerp_mix(T rhs, float t) { return rhs * t; }
template<bool useAlpha, typename T> static inline T lerp_mix(T rhs, T lhs, float t) { return mix(rhs, lhs, t); }

template<bool useAlpha, typename T> static inline T blend_mix(T rhs, float t) { return rhs * t; }
template<bool useAlpha, typename T> static inline T blend_mix(T rhs, T lhs, float t) { return mix(rhs, lhs, t); }
template<> inline vector_float4 blend_mix<true, vector_float4>(vector_float4 rhs, vector_float4 lhs, float t) {
  if (t < 0.5f) {
    float a = lhs.a * t * 2.0f;
    vector_float3 rgb = mix(vector_float3 { rhs.x, rhs.y, rhs.z }, vector_float3 { lhs.x, lhs.y, lhs.z }, a);
    float aOut = mix(rhs.a, 1.0f, a);
    return vector_float4 { rgb.x, rgb.y, rgb.z, aOut };
  } else {
    float a = t * 2.0f - 1.0f;
    vector_float3 rgb = mix(vector_float3 { rhs.x, rhs.y, rhs.z } * (1.0f - a), vector_float3 { lhs.x, lhs.y, lhs.z }, lhs.a);
    float aOut = mix(mix(rhs.a, 1.0f, lhs.a), lhs.a, a);
    return vector_float4 { rgb.x, rgb.y, rgb.z, aOut };
  }
}

template<bool useAlpha, typename T> static inline T add_mix(T rhs, float t) { return rhs * t; }
template<bool useAlpha, typename T> static inline T add_mix(T rhs, T lhs, float t) { return rhs + lhs * t; }


kernel void CopyTextureSampleNearest(
    NanoTexture input [[texture(0)]],
    NanoWriteTexture output [[texture(1)]],
    vector_uint2 gid_xy_uint [[thread_position_in_grid]]) {
  constexpr sampler sampler(coord::pixel, filter::nearest, address::clamp_to_edge);
  output.write(input.sample(sampler, vector_float2(gid_xy_uint)), gid_xy_uint);
}

kernel void CopyTextureSampleLinear(
    NanoTexture input [[texture(0)]],
    NanoWriteTexture output [[texture(1)]],
    vector_uint2 gid_xy_uint [[thread_position_in_grid]],
    vector_uint2 size_xy_uint [[threads_per_grid]]) {
  vector_float2 gid_xy_norm = (float2(gid_xy_uint) + 0.5) / float2(size_xy_uint);
  constexpr sampler sampler(coord::normalized, filter::linear, address::clamp_to_edge);
  output.write(input.sample(sampler, gid_xy_norm), gid_xy_uint);
}

kernel void ClearTextureSolid(
    constant const vector_float4& input [[buffer(0)]],
    NanoWriteTexture output [[texture(1)]],
    vector_uint2 gid_xy_uint [[thread_position_in_grid]]) {
  output.write(half4(input), gid_xy_uint);
}




#include "Generated/Program.metal.incl.h"

