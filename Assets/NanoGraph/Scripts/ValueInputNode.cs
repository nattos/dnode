using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class ValueInputNode : ScalarComputeNode {
    public enum InputType {
      Int,
      Float,
    }

    [EditableAttribute]
    public InputType Type = InputType.Float;

    [EditableAttribute]
    public double DefaultValue = 0.0;
    [EditableAttribute]
    public double MinValue = 0.0;
    [EditableAttribute]
    public double MaxValue = 1.0;

    public override DataSpec InputSpec => DataSpec.Empty;
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakePrimitive("Out", ValueType));

    public PrimitiveType ValueType {
      get {
        switch (Type) {
          case InputType.Int:
            return PrimitiveType.Int;
          case InputType.Float:
          default:
            return PrimitiveType.Float;
        }
      }
    }

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    private class EmitterInput : EmitterCpu {
      public new ValueInputNode Node;
      public int valueInputKey;

      public EmitterInput(ValueInputNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionPreamble(out NanoFunction func) {
        base.EmitFunctionPreamble(out func);
        this.valueInputKey = program.AllocateValueInput(Node.ShortName, Node.DefaultValue, Node.MinValue, Node.MaxValue);
      }

      public override void EmitValidateCacheFunctionInner() {
        base.EmitValidateCacheFunctionInner();
        string inputExpr = $"GetValueInput({validateCacheFunction.EmitLiteral(valueInputKey)})";
        var fieldName = resultType.GetField("Out");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName} = ({validateCacheFunction.GetTypeIdentifier(Node.ValueType)}){inputExpr};");
      }
    }
  }
}
