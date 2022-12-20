using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public struct CodeLocal {
    public string Identifier;
    public TypeSpec Type;
  }

  public struct CodeContext {
    public NanoFunction Function;
    public IReadOnlyList<CodeLocal> InputLocals;
    public IReadOnlyList<CodeLocal> OutputLocals;
    public List<string> Errors;
  }

  public struct CodeCachedResult {
    public NanoProgramType ResultType;
    public CodeLocal Result;
  }

  public interface ICodeNode : IDataNode {
    void EmitCode(CodeContext context);
  }

  public interface IConditionalNode : ICodeNode {
    void GetInputsAreConditional(bool[] outInputsAreConditional);
    void EmitInputsUsedCode(CodeContext context);
  }

  public struct ComputeNodeResultEntry {
    public IComputeNode Node;
    public IComputeNodeEmitCodeOperation Operation;
    public CodeCachedResult? Result;
  }

  public struct ComputeNodeEmitCodeOperationContext {
    public List<string> errors;
    public NanoGraph graph;
    public NanoProgram program;

    public NanoFunction executeFunction;
    public NanoFunction createPipelinesFunction;

    public IReadOnlyList<ComputeNodeResultEntry> dependentComputeNodes;
    public IReadOnlyList<DataPlug> dependentComputeInputs;
  }

  public interface IComputeNodeEmitCodeOperation {
    void EmitFunctionSignature();
    void EmitFunctionPreamble(out NanoFunction func);
    void EmitLoadFunctionInputs();
    void ConsumeFunctionBodyResult(IReadOnlyDictionary<DataPlug, CodeLocal> resultLocalMap);
    void EmitFunctionReturn(out CodeCachedResult? result);
    void EmitValidateCacheFunction();
    void EmitExecuteFunctionCode();

    void RecordLoadInputForDescendantNode(IComputeNode descendant, DataField field, int inputIndex);
    DataField[] GetInputsForDescendantNode(IComputeNode descendant);
    void EmitLoadInputsForDescendantNode(IComputeNode descendant, CodeContext context);
  }

  public interface IComputeNode : IDataNode {
    DataSpec ComputeInputSpec { get; }
    DataSpec ComputeOutputSpec { get; }
    INanoCodeContext CodeContext { get; }
    IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context);
  }

  public interface ICompileTimeOnlyNode : IDataNode {}

  public interface IInternalNode : IDataNode {
    bool IsInternal { get; }
  }
}
