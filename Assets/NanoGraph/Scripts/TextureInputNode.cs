using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class TextureInputNode : ScalarComputeNode {
    public override DataSpec InputSpec => DataSpec.Empty;
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakePrimitive("Out", PrimitiveType.Texture));

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    private class EmitterInput : EmitterCpu {
      public new TextureInputNode Node;
      public int textureInputIndex;

      public EmitterInput(TextureInputNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionPreamble(out NanoFunction func) {
        base.EmitFunctionPreamble(out func);
        this.textureInputIndex = program.AllocateTextureInput();
      }

      public override void EmitValidateCacheFunction() {
        base.EmitValidateCacheFunction();
        string inputExpr = $"GetTextureInput({validateCacheFunction.EmitLiteral(textureInputIndex)})";
        var fieldName = resultType.GetField("Out");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName} = {inputExpr};");
      }
    }
  }
}
