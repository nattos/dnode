using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class AudioInputNode : ScalarComputeNode {
    protected override string ShortNamePart => "AudioInput";

    public override DataSpec InputSpec => DataSpec.FromFields();
    public override DataSpec OutputSpec => DataSpec.FromFields(
      DataField.FromTypeField(TypeField.ToArray(TypeField.MakePrimitive("Samples", PrimitiveType.Float), true)),
      DataField.MakePrimitive("SampleRate", PrimitiveType.Float),
      DataField.MakePrimitive("NextWriteIndex", PrimitiveType.Int)
    );

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    private class EmitterInput : EmitterCpu {
      public new AudioInputNode Node;
      public string audioInputStreamInstanceFieldIdentifier;

      public EmitterInput(AudioInputNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionPreamble(out NanoFunction func) {
        base.EmitFunctionPreamble(out func);

        program.AddCustomInclude("NanoAudioInputStream.h");

        NanoProgramType audioInputStreamType = NanoProgramType.MakeBuiltIn(program, "std::unique_ptr<NanoAudioInputStream>");
        this.audioInputStreamInstanceFieldIdentifier = program.AddInstanceField(audioInputStreamType, $"{Node.ShortName}_Stream");

        createPipelinesFunction.AddStatement($"{this.audioInputStreamInstanceFieldIdentifier}.reset(new NanoAudioInputStream());");
        createPipelinesFunction.AddStatement($"{this.audioInputStreamInstanceFieldIdentifier}->Start();");
      }

      public override void EmitValidateCacheFunctionInner() {
        // base.EmitValidateCacheFunctionInner();

        validateCacheFunction.AddStatement($"float* rawBuffer = {this.audioInputStreamInstanceFieldIdentifier}->GetBuffer();");
        validateCacheFunction.AddStatement($"int rawBufferLength = {this.audioInputStreamInstanceFieldIdentifier}->GetBufferLength();");
        validateCacheFunction.AddStatement($"int nextWriteIndex = {this.audioInputStreamInstanceFieldIdentifier}->GetBufferNextWriteIndex();");
        validateCacheFunction.AddStatement($"float sampleRate = {this.audioInputStreamInstanceFieldIdentifier}->GetSampleRate();");
        validateCacheFunction.AddStatement($"std::shared_ptr<NanoTypedBuffer<float>> buffer(new NanoTypedBuffer<float>(rawBuffer, rawBufferLength));");
        validateCacheFunction.AddStatement($"buffer->MarkCpuBufferChanged();");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{resultType.GetField("Samples")} = buffer;");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{resultType.GetField("SampleRate")} = sampleRate;");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{resultType.GetField("NextWriteIndex")} = nextWriteIndex;");
      }
    }
  }
}
