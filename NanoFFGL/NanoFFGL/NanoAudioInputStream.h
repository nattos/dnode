#pragma once

#import <AVFoundation/AVFoundation.h>

class NanoAudioInputStream {
  static constexpr int TargetSampleRate = 44100;
  static constexpr int BufferLength = 1024 * 64;

public:
  ~NanoAudioInputStream();
  void Start();
  void Stop();
  
  const float* GetBuffer() const { return _buffer; }
  float* GetBuffer() { return _buffer; }
  int GetBufferLength() const { return BufferLength; }
  float GetSampleRate() const { return _sampleRate; }
  int GetBufferNextWriteIndex() const { return _bufferWritePos; }

private:
  void ProcessSamples(
      const AudioTimeStamp* inNow,
      const AudioBufferList* inInputData,
      const AudioTimeStamp* inInputTime,
      AudioBufferList* outOutputData,
      const AudioTimeStamp* inOutputTime);
  static OSStatus AudioHandlerProc(
      AudioObjectID inDevice,
      const AudioTimeStamp* inNow,
      const AudioBufferList* inInputData,
      const AudioTimeStamp* inInputTime,
      AudioBufferList* outOutputData,
      const AudioTimeStamp* inOutputTime,
      void* __nullable inClientData);
  template<typename T> void DecimateSamplesToBuffer(const AudioBuffer& inputBuffer);

  bool _isStarted = false;
  bool _hasDefaultInputDevice = false;
  AudioObjectID _defaultInputDevice = 0;
  AudioDeviceIOProcID _audioProcId = nullptr;
  float _deviceSampleRate = 44100.0f;
  float _sampleRate = 44100.0f;
  int _decimateRate = 1;
  int _decimateIndex = 0;
  bool _formatIsFloat = false;
  int _formatBitsPerChannel = 0;
  float _buffer[BufferLength] = { 0, };
  int _bufferWritePos = 0;
};
