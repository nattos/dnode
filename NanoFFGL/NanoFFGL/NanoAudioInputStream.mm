#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <CoreAudio/AudioHardware.h>

#include "NanoAudioInputStream.h"


#define OPTIONAL_GET_OR_RETURN(x, y, z) x y; { auto tmp = (z); if (!tmp) { return; } y = tmp.value(); }
#define TRUE_OR_RETURN(x) if (!(x)) { return; }

namespace {

template<typename T, T TMaxValue, T TMinValue> static float ConvertSampleInt(T value) {
  constexpr bool TNormalize = true;
  if (TNormalize) {
    if (value >= 0) {
      return value / (float)TMaxValue;
    } else {
      return -(value / (float)TMinValue);
    }
  } else {
    return value;
  }
}

template<typename T> static float ConvertSample(T value) {
  return (float)value;
}

template<> float ConvertSample<int8_t>(int8_t value) {
  return ConvertSampleInt<int8_t, std::numeric_limits<int8_t>::lowest(), std::numeric_limits<int8_t>::max()>(value);
}

template<> float ConvertSample<int16_t>(int16_t value) {
  return ConvertSampleInt<int16_t, std::numeric_limits<int16_t>::lowest(), std::numeric_limits<int16_t>::max()>(value);
}

template<> float ConvertSample<int32_t>(int32_t value) {
  return ConvertSampleInt<int32_t, std::numeric_limits<int32_t>::lowest(), std::numeric_limits<int32_t>::max()>(value);
}

template<typename T> static std::optional<T> CoreAudioReadProperty(AudioObjectID audioObjectID, AudioObjectPropertySelector property, AudioObjectPropertyScope scope = kAudioObjectPropertyScopeGlobal) {
  AudioObjectPropertyAddress propertyAddress;
  propertyAddress.mSelector = property;
  propertyAddress.mScope = scope;
  propertyAddress.mElement = kAudioObjectPropertyElementMain;

  UInt32 propertySize = 0;
  if (AudioObjectGetPropertyDataSize(audioObjectID, &propertyAddress, 0, nullptr, &propertySize) != noErr) {
    return std::nullopt;
  }
  if (propertySize != sizeof(T)) {
    return std::nullopt;
  }

  T result;
  if (AudioObjectGetPropertyData(audioObjectID, &propertyAddress, 0, nullptr, &propertySize, &result)) {
    return std::nullopt;
  }
  return result;
}

template<typename T> static bool CoreAudioSetProperty(AudioObjectID audioObjectID, AudioObjectPropertySelector property, const T& value, AudioObjectPropertyScope scope = kAudioObjectPropertyScopeGlobal) {
  AudioObjectPropertyAddress propertyAddress;
  propertyAddress.mSelector = property;
  propertyAddress.mScope = scope;
  propertyAddress.mElement = kAudioObjectPropertyElementMain;

  if (AudioObjectSetPropertyData(audioObjectID, &propertyAddress, 0, nullptr, sizeof(T), &value)) {
    return false;
  }
  return true;
}

}




NanoAudioInputStream::~NanoAudioInputStream() {
  Stop();
}

void NanoAudioInputStream::Start() {
  if (_isStarted) {
    return;
  }
  _isStarted = true;
  _hasDefaultInputDevice = false;

//    OPTIONAL_GET_OR_RETURN(
//        AudioObjectID, defaultOutputDevice,
//        CoreAudioReadProperty<AudioObjectID>(kAudioObjectSystemObject, kAudioHardwarePropertyDefaultOutputDevice));
  OPTIONAL_GET_OR_RETURN(
      (void), _defaultInputDevice,
      CoreAudioReadProperty<AudioObjectID>(kAudioObjectSystemObject, kAudioHardwarePropertyDefaultInputDevice));
  _hasDefaultInputDevice = true;
  OPTIONAL_GET_OR_RETURN(
      double, inputDeviceSampleRate,
      CoreAudioReadProperty<double>(_defaultInputDevice, kAudioDevicePropertyNominalSampleRate));

  _deviceSampleRate = (float)inputDeviceSampleRate;
  _decimateRate = std::max(1, (int)std::floor(inputDeviceSampleRate / TargetSampleRate));
  _sampleRate = (float)(inputDeviceSampleRate / _decimateRate);
//  int _decimateIndex = 0;
//  float _buffer[BufferLength] = { 0, };
//  int _bufferWritePos = 0;

//  AudioStreamBasicDescription desiredAudioFormat;
//  FillOutASBDForLPCM(desiredAudioFormat, 44100, 1, 16, 16, false, false, false);
//  TRUE_OR_RETURN(CoreAudioSetProperty(defaultInputDevice, kAudioStreamPropertyVirtualFormat, desiredAudioFormat, kAudioObjectPropertyScopeInput));
//  TRUE_OR_RETURN(CoreAudioSetProperty<double>(defaultInputDevice, kAudioDevicePropertyNominalSampleRate, 44100.0f));
//  TRUE_OR_RETURN(CoreAudioSetProperty<double>(defaultInputDevice, kAudioDevicePropertyNominalSampleRate, 44100.0f, kAudioObjectPropertyScopeInput));

  OPTIONAL_GET_OR_RETURN(
      AudioStreamBasicDescription, inputDeviceFormat,
      CoreAudioReadProperty<AudioStreamBasicDescription>(_defaultInputDevice, kAudioStreamPropertyVirtualFormat, kAudioObjectPropertyScopeInput));
  _formatIsFloat = inputDeviceFormat.mFormatFlags & kAudioFormatFlagIsFloat;
  _formatBitsPerChannel = inputDeviceFormat.mBitsPerChannel;

  if (AudioDeviceCreateIOProcID(_defaultInputDevice, AudioHandlerProc, this, &_audioProcId) != noErr) {
    return;
  }
  AudioDeviceStart(_defaultInputDevice, _audioProcId);
}

void NanoAudioInputStream::Stop() {
  if (!_isStarted) {
    return;
  }
  _isStarted = false;

  if (_hasDefaultInputDevice && _audioProcId) {
    AudioDeviceStop(_defaultInputDevice, _audioProcId);
    AudioDeviceDestroyIOProcID(_defaultInputDevice, _audioProcId);
  }
  _audioProcId = nullptr;
  _hasDefaultInputDevice = false;
  _defaultInputDevice = 0;
}

void NanoAudioInputStream::ProcessSamples(
    const AudioTimeStamp* inNow,
    const AudioBufferList* inInputData,
    const AudioTimeStamp* inInputTime,
    AudioBufferList* outOutputData,
    const AudioTimeStamp* inOutputTime) {
  if (!inInputData || inInputData->mNumberBuffers == 0) {
    return;
  }
  const auto& inputBuffer = inInputData->mBuffers[0];
  if (_formatIsFloat) {
    switch (_formatBitsPerChannel) {
      case 8:
        DecimateSamplesToBuffer<int8_t>(inputBuffer);
        break;
      case 16:
        DecimateSamplesToBuffer<int16_t>(inputBuffer);
        break;
      // TODO: 24bits :(
      case 32:
        DecimateSamplesToBuffer<int32_t>(inputBuffer);
        break;
    }
  } else {
    switch (_formatBitsPerChannel) {
      case 32:
        DecimateSamplesToBuffer<float>(inputBuffer);
        break;
      case 64:
        DecimateSamplesToBuffer<double>(inputBuffer);
        break;
    }
  }
}

OSStatus NanoAudioInputStream::AudioHandlerProc(
    AudioObjectID inDevice,
    const AudioTimeStamp* inNow,
    const AudioBufferList* inInputData,
    const AudioTimeStamp* inInputTime,
    AudioBufferList* outOutputData,
    const AudioTimeStamp* inOutputTime,
    void* __nullable inClientData) {
  if (!inClientData) {
    return noErr;
  }
  ((NanoAudioInputStream*)inClientData)->ProcessSamples(inNow, inInputData, inInputTime, outOutputData, inOutputTime);
  return noErr;
}

template<typename T> void NanoAudioInputStream::DecimateSamplesToBuffer(const AudioBuffer& inputBuffer) {
  const int channels = inputBuffer.mNumberChannels;
  const int byteCount = inputBuffer.mDataByteSize;
  const int sampleCount = byteCount / channels / sizeof(T);
  T* samples = (T*)inputBuffer.mData;
  float firstSample = ConvertSample(samples[0]);
  float lastSample = ConvertSample(samples[(sampleCount - 1) * channels]);

  int writeCount = (sampleCount - _decimateIndex) / _decimateRate;
  for (int i = 0; i < writeCount; ++i) {
    _buffer[(_bufferWritePos++) & (BufferLength - 1)] = ConvertSample(samples[(i * _decimateRate + _decimateIndex) * channels]);
  }

  _decimateIndex = (_decimateIndex + (sampleCount % _decimateRate)) % _decimateRate;
  NSLog(@"Received sampleCount: %d firstSample: %f lastSample: %f _buffer[0]: %f _decimateIndex: %d writeCount: %d", sampleCount, firstSample, lastSample, _buffer[0], _decimateIndex, writeCount);
}
