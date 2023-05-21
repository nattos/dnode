using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class ReadBufferRefNode : ScalarComputeNode {
    protected override string ShortNamePart => $"ReadBufferRef";

    public override DataSpec InputSpec => DataSpec.FromFields(new DataField { Name = "BufferRef", IsCompileTimeOnly = true, Type = TypeSpec.MakePrimitive(PrimitiveType.BufferRef) });

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    protected override bool TryGetFieldForBufferRefFieldName(string bufferRefFieldName, out string fieldName) {
      if (bufferRefFieldName == "BufferRef") {
        fieldName = "Out";
        return true;
      }
      fieldName = null;
      return false;
    }
    protected override bool TryGetBufferRefFieldNameForField(string fieldName, out string bufferRefFieldName) {
      if (fieldName == "Out") {
        bufferRefFieldName = "BufferRef";
        return true;
      }
      bufferRefFieldName = null;
      return false;
    }

    private class EmitterInput : EmitterCpu {
      public new ReadBufferRefNode Node;
      public string instanceFieldIdentifier;

      public EmitterInput(ReadBufferRefNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionReturn(out CodeCachedResult? result) {
        var bufferRefInput = this.collectedComputeInputs.FirstOrDefault();
        func.AddStatement($"return {{ {bufferRefInput.Expression} }};");
        result = new CodeCachedResult { ResultType = resultType, Result = new CodeLocal { Identifier = cachedResult.Identifier, Type = resultTypeSpec } };
      }

      public override void EmitValidateCacheFunctionInner() {
        validateCacheFunction.AddStatement($"{cachedResult.Identifier} = {func.Identifier}();");
      }
    }
  }
}
