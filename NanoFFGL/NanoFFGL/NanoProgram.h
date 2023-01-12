#pragma once

class NanoSharedTexture;

struct BlitOutputTextureResources {
  id<MTLComputePipelineState> CopyTextureSampleNearestPipeline;
  id<MTLComputePipelineState> CopyTextureSampleLinearPipeline;
  id<MTLComputePipelineState> ClearTextureSolidPipeline;
};

class NanoProgram {
public:
  struct ParameterDecl {
    std::string Name;
    double DefaultValue;
    double MinValue;
    double MaxValue;
  };

  struct DebugValue {
    std::string Key;
    int Length;
    vector_double4 Value;
  };

  struct DebugSettableValue {
    std::string Key;
    std::function<void(const std::vector<double>&)> Setter;
  };

  NanoProgram();
  virtual ~NanoProgram();

  vector_int2 OutputTextureSize = vector_int2 { 1920, 1080 };

  void Run();
  void EnsureResources();

  virtual std::vector<ParameterDecl> GetParameterDecls() = 0;
  virtual std::vector<DebugValue> GetDebugValues() = 0;
  virtual std::vector<DebugSettableValue> GetDebugSettableValues() = 0;

  void SetupParameters();
  
  int GetValueInputCount() const;
  double GetValueInput(int index) const;
  void SetValueInput(int index, double value);

  // TODO: Should be const, but generator code doesn't yet support tagging functions with const.
  virtual int GetTextureInputCount() = 0;
  id<MTLTexture> GetTextureInput(int index) const;
  void SetTextureInput(int index, id<MTLTexture> value);
  void BlitOutputTexture(int index, id<MTLTexture> outputTexture);
  
  int32_t DebugGetOutputTextureSurfaceId() const;
  const std::string& DebugGetOutputTextureKey() const { return _debugOutputTextureKey; }
  void DebugSetOutputTextureKey(const std::string& value) { _debugOutputTextureKey = value; }

  id<MTLDevice> GetDevice() const { return _device; }
  void SetDevice(id<MTLDevice> value) { _device = value; }
  id<MTLCommandQueue> GetCommandQueue() const { return _commandQueue; }
  void SetCommandQueue(id<MTLCommandQueue> value) { _commandQueue = value; }
  id<MTLCommandBuffer> GetCurrentCommandBuffer() const { return _currentCommandBuffer; }

  int GetFrameNumber() const { return _frameNumber; }
  double GetFrameTime() const { return _frameTime; }

  static void SetCurrentInstance(NanoProgram* ptr);
  static NanoProgram* GetCurrentInstance();

protected:
  virtual void Execute() = 0;
  virtual void CreatePipelines() = 0;

  id<MTLTexture> ResizeTexture(id<MTLTexture> originalTexture, int width, int height, MTLPixelFormat format);
  static void ResizeSharedTexture(std::unique_ptr<NanoSharedTexture>& ptr, id<MTLDevice> device, int32_t width, int32_t height);

private:
  id<MTLDevice> _device;
  id<MTLCommandQueue> _commandQueue;
  id<MTLCommandBuffer> _currentCommandBuffer;
  bool _createdPipelines = false;
  std::vector<double> _valueInputs;
  std::vector<id<MTLTexture>> _inputTextures;
  std::string _debugOutputTextureKey;

  BlitOutputTextureResources _blitOutputTextureResources;

  int _frameNumber = 0;
  double _startTime = 0;
  double _frameTime = 0;

  static NSLock* _threadMapLock;
  static std::unique_ptr<std::map<NSThread*, NanoProgram*>> _threadMap;

protected:
  std::unique_ptr<NanoSharedTexture> _debugOutputTexture;
};

NanoProgram* CreateProgram();
