using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class ReadBufferRefNode : ScalarComputeNode {
    protected override string ShortNamePart => $"ReadBufferRef";

    [EditableAttribute]
    public bool IsGpuContext = false;

    public override DataSpec InputSpec => DataSpec.FromFields(new DataField { Name = "BufferRef", IsCompileTimeOnly = true, Type = TypeSpec.MakePrimitive(PrimitiveType.BufferRef) });

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    private class EmitterInput : EmitterCpu {
      public new ReadBufferRefNode Node;
      public string instanceFieldIdentifier;

      public EmitterInput(ReadBufferRefNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        var bufferRefInput = this.collectedComputeInputs.FirstOrDefault();
        func.AddStatement($"return {bufferRefInput.Expression};");
        result = new CodeCachedResult { ResultType = resultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec } };
      }

      public override void EmitValidateCacheFunctionInner() {
        validateCacheFunction.AddStatement($"{cachedResult.Identifier} = {func.Identifier}();");
      }
    }
  }
}
