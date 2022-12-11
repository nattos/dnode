using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public interface ISplitComputeNode : IComputeNode {
  }

  public interface ISplitComputeNodeEmitCodeOperation : IComputeNodeEmitCodeOperation {
    void EmitLateUpdateCode(IComputeNode fromNode, CodeContext context);
    void EmitPreValidateCache(NanoFunction validateCacheFunc, IComputeNode fromNode, string threadCountExpr);
    void EmitPostValidateCache(NanoFunction validateCacheFunc, IComputeNode fromNode);
  }

  public class LatchNode : ScalarComputeNode, ISplitComputeNode {
    [EditableAttribute]
    public bool IsGpuContext = false;

    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakeType("In", ValueType));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakeType("Out", ValueType));
    public override DataSpec ComputeOutputSpec => DataSpec.FromFields(DataField.MakeType("Out", StorageType));

    public TypeSpec ValueType {
      get {
        return TypeSpec.MakePrimitive(PrimitiveType.Float);
      }
    }

    public TypeSpec StorageType => IsGpuContext ? TypeSpec.MakeArray(ValueType) : ValueType;

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    private class EmitterInput : EmitterCpu, ISplitComputeNodeEmitCodeOperation {
      public new LatchNode Node;
      public string instanceFieldIdentifier;

      public EmitterInput(LatchNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public void EmitLateUpdateCode(IComputeNode fromNode, CodeContext context) {
        if (Node.IsGpuContext) {
          if (!descendantInputs.TryGetValue(fromNode, out var inputs) || inputs.Count == 0) {
            context.Errors.Add($"Node {Node} cannot update because input from {fromNode} is not ready.");
            return;
          }
          int? inputIndex = inputs.FirstOrNull(field => field.field.Name == "Out")?.inputIndex;
          if (inputIndex == null) {
            context.Errors.Add($"Node {Node} cannot update because input from {fromNode} is not ready.");
            return;
          }
          context.Function.AddStatement($"{context.Function.Context.EmitWriteBuffer($"input{inputIndex}", "gid", context.InputLocals[0].Identifier)};");
          // Note: No need to mark CPU buffer dirty since it is not used.
        } else {
          // TODO: Handle copies of buffers correctly.
          var resultType = context.Function.Program.GetProgramType(Node.StorageType, Node.ShortName);
          context.Function.AddStatement($"{instanceFieldIdentifier} = {context.InputLocals[0].Identifier};");
        }
      }

      public void EmitPreValidateCache(NanoFunction validateCacheFunc, IComputeNode fromNode, string threadCountExpr) {
        if (!Node.IsGpuContext) {
          return;
        }
        // TODO: Allow the CPU buffer to not exist at all.
        validateCacheFunc.AddStatement($"{instanceFieldIdentifier}->Resize({threadCountExpr});");
        // Note: We don't care about the contents of the CPU buffer at all.
        validateCacheFunc.AddStatement($"{instanceFieldIdentifier}->MarkGpuBufferChanged();");
      }

      public void EmitPostValidateCache(NanoFunction validateCacheFunc, IComputeNode fromNode) {
        if (!Node.IsGpuContext) {
          return;
        }
      }

      public override void EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc) {
        base.EmitFunctionPreamble(out func, out arraySizesFunc);
        this.instanceFieldIdentifier = program.AddInstanceField(program.GetProgramType(Node.StorageType, "Latch"), Node.ShortName);
      }

      public override void EmitValidateCacheFunction() {
        base.EmitValidateCacheFunction();
        var fieldName = resultType.GetField("Out");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName} = {instanceFieldIdentifier};");

        if (Node.IsGpuContext) {
          createPipelinesFunction.AddStatement($"{instanceFieldIdentifier}.reset(NanoTypedBuffer<{createPipelinesFunction.GetTypeIdentifier(Node.ValueType)}>::Allocate(0));");
        }
      }
    }
  }
}
