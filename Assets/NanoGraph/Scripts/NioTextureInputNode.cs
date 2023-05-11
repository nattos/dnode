using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class NioTextureInputNode : ScalarComputeNode {
    public override DataSpec InputSpec => DataSpec.FromFields(DataField.MakePrimitive("Offset", PrimitiveType.Int));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakePrimitive("Out", PrimitiveType.Texture));
    protected override string ShortNamePart => $"NioTextureIn";

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    private class EmitterInput : EmitterCpu {
      public new NioTextureInputNode Node;

      public EmitterInput(NioTextureInputNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionPreamble(out NanoFunction func) {
        base.EmitFunctionPreamble(out func);
      }

      public override void EmitValidateCacheFunctionInner() {
        base.EmitValidateCacheFunctionInner();
        string inputExpr = $"GetTextureFromStack((int){GetInputExpr("Offset")})";
        var fieldName = resultType.GetField("Out");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName} = {inputExpr};");
      }

      private string GetInputExpr(string fieldName) {
        var edge = graph.GetEdgeToDestinationOrNull(computeNode, fieldName);
        if (edge == null) {
          return null;
        }
        if (!(edge.Source.Node is IComputeNode sourceComputeNode)) {
          NanoGraph.CurrentGenerateState.AddError($"Node {computeNode} depends on an output that is not a compute node ({edge.Source.Node}).");
          return null;
        }
        CodeCachedResult? sourceCachedResult = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == sourceComputeNode)?.Result;
        if (sourceCachedResult == null) {
          NanoGraph.CurrentGenerateState.AddError($"Node {computeNode} depends on a compute node that is not yet ready ({edge.Source.Node}).");
          return null;
        }
        string inputExpr = $"{sourceCachedResult?.Result.Identifier}.{sourceCachedResult?.ResultType.GetField(edge.Source.FieldName)}";
        return inputExpr;
      }
    }
  }
}
