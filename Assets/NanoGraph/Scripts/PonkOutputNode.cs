using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public class PonkOutputNode : ScalarComputeNode {
    public override DataSpec InputSpec => DataSpec.FromFields(
        DataField.MakeType("Counter", TypeSpec.MakeArray(TypeSpec.MakePrimitive(PrimitiveType.Int))),
        DataField.MakeType("PathPoints", TypeSpec.MakeArray(TypeSpec.MakePrimitive(PrimitiveType.Float2))),
        DataField.MakeType("PathIndexes", TypeSpec.MakeArray(TypeSpec.MakePrimitive(PrimitiveType.Int))));
    public override DataSpec OutputSpec => DataSpec.FromFields(DataField.MakePrimitive("Placeholder", PrimitiveType.Int));

    public override IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context) => new EmitterInput(this, context);

    private class EmitterInput : EmitterCpu {
      public new PonkOutputNode Node;
      public string counterInstanceFieldIdentifier;
      public string pathPointsInstanceFieldIdentifier;
      public string pathIndexesInstanceFieldIdentifier;

      public EmitterInput(PonkOutputNode node, ComputeNodeEmitCodeOperationContext context) : base(node, context) {
        Node = node;
      }

      public override void EmitFunctionPreamble(out NanoFunction func) {
        base.EmitFunctionPreamble(out func);
        NanoProgramType ponkDataType = NanoProgramType.MakeBuiltIn(program, "std::vector<std::vector<vector_float2>>");
        this.counterInstanceFieldIdentifier = program.AllocateBufferOutput($"{Node.ShortName}_Counter");
        this.pathPointsInstanceFieldIdentifier = program.AllocateBufferOutput($"{Node.ShortName}_PathPoints");
        this.pathIndexesInstanceFieldIdentifier = program.AllocateBufferOutput($"{Node.ShortName}_PathIndexes");
      }

      public override void EmitValidateCacheFunctionInner() {
        base.EmitValidateCacheFunctionInner();

        validateCacheFunction.AddStatement($"{this.counterInstanceFieldIdentifier} = {GetInputExpr("Counter")}->GetGpuBuffer();");
        validateCacheFunction.AddStatement($"{this.pathPointsInstanceFieldIdentifier} = {GetInputExpr("PathPoints")}->GetGpuBuffer();");
        validateCacheFunction.AddStatement($"{this.pathIndexesInstanceFieldIdentifier} = {GetInputExpr("PathIndexes")}->GetGpuBuffer();");

        var fieldName = resultType.GetField("Placeholder");
        validateCacheFunction.AddStatement($"{cachedResult.Identifier}.{fieldName} = 0;");
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
