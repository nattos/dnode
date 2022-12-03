using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public struct CodeLocal {
    public string Identifier;
    public TypeSpec Type;
    public string ArraySizeIdentifier;
  }

  public struct CodeContext {
    public NanoFunction Function;
    public NanoFunction ArraySizeFunction;
    public IReadOnlyList<CodeLocal> InputLocals;
    public IReadOnlyList<DataField> CompileTimeOnlyInputs;
    public IReadOnlyList<CodeLocal> OutputLocals;
    public List<string> Errors;
  }

  public struct CodeCachedResult {
    public NanoProgramType ResultType;
    public NanoProgramType ArraySizesResultType;
    public CodeLocal Result;
  }

  public interface ICodeNode : IDataNode {
    void EmitCode(CodeContext context);
  }

  public interface IComputeNode : IDataNode {
    DataSpec ComputeInputSpec { get; }
    DataSpec AuxSizesOutputSpec { get; }
    INanoCodeContext CodeContext { get; }
    void EmitLoadOutputsCode(CodeContext context, CodeCachedResult cachedResult);
    void EmitStoreAuxSizesCode(CodeContext context, CodeCachedResult cachedResult);
    string EmitTotalThreadCount(NanoFunction func, CodeCachedResult cachedResult);
  }

  public interface ICompileTimeOnlyNode : IDataNode {}
}
