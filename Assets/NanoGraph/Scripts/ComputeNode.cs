using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum ExecutionMode {
    // Cache and update at most once a frame, only if the output is used.
    OncePerFrame,
    // Cache and update at once a frame, even if the result is not used.
    OncePerFrameAlways,
    // Execute and cache first time requested.
    Once,
    // Execute and cache at program start.
    OnceOnStart,
    // Only update when trigger signal sent, or first time requested.
    OnTrigger,
    // Do not cache. Executes everytime value is polled.
    OnDemand,
  }

  public enum ExecuteFunctionContextType {
    GlobalFrameStep,
    OnDemand,
  }

  public enum FieldPortsMode {
    Combined,
    Individual,
  }

  public enum TypeDeclMode {
    Internal,
    External,
  }

  public abstract class ComputeNode : DataNode, IComputeNode {
    [EditableAttribute]
    public ExecutionMode ExecutionMode = ExecutionMode.OncePerFrame;

    [EditableAttribute]
    public FieldPortsMode InputPortsMode = FieldPortsMode.Individual;
    [EditableAttribute]
    public FieldPortsMode OutputPortsMode = FieldPortsMode.Individual;

    public TypeDeclMode TypeDeclMode = TypeDeclMode.External;
    public TypeDecl InternalType;

    protected virtual PrimitiveType? SingleFieldModeType => null;
    public bool IsSingleFieldMode => SingleFieldModeType != null;
    protected virtual bool RequiresTypeDeclInput => !IsSingleFieldMode;

    public virtual DataSpec ComputeInputSpec => DataSpec.ExtendWithFields(TypeDeclFields, GetInputOutputDataSpec(FieldPortsMode.Combined, InputTypeFields));
    public virtual DataSpec ComputeOutputSpec => OutputSpec;
    public override DataSpec InputSpec => DataSpec.ExtendWithFields(TypeDeclFields, GetInputOutputDataSpec(InputPortsMode, InputTypeFields));
    public override DataSpec OutputSpec => GetInputOutputDataSpec(OutputPortsMode, OutputTypeFields);

    private DataSpec GetInputOutputDataSpec(FieldPortsMode fieldsMode, TypeField[] fields) {
      switch (fieldsMode) {
        case FieldPortsMode.Combined:
        default:
          return DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeType(new TypeDecl(fields))));
        case FieldPortsMode.Individual:
          return DataSpec.FromTypeFields(fields);
      }
    }

    protected DataField[] TypeDeclFields => !RequiresTypeDeclInput ? Array.Empty<DataField>() : new[] { new DataField { Name = "TypeDecl", IsCompileTimeOnly = true, Type = TypeSpec.MakePrimitive(PrimitiveType.TypeDecl) } };

    public virtual TypeField[] InputTypeFields => InputOutputTypeFields;
    public virtual TypeField[] OutputTypeFields => InputOutputTypeFields;

    public virtual TypeField[] InputOutputTypeFields {
      get {
        switch (TypeDeclMode) {
          case TypeDeclMode.Internal:
          default:
            return InternalType.Fields.ToArray();
          case TypeDeclMode.External: {
            if (IsSingleFieldMode) {
              PrimitiveType fieldType = SingleFieldModeType.Value;
              return new[] { TypeField.MakePrimitive("Out", fieldType) };
            }
            TypeSpec? field = Graph.GetEdgeToDestinationOrNull(new DataPlug { Node = this, FieldName = "TypeDecl" })?.SourceFieldOrNull?.Type;
            TypeDecl inputType = field?.Type ?? TypeDecl.Empty;
            return inputType.Fields.ToArray();
          }
        }
      }
    }

    public abstract INanoCodeContext CodeContext { get; }
    public abstract IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context);


    public abstract class EmitterBase : IComputeNodeEmitCodeOperation {
      public EmitterBase(ComputeNode node, ComputeNodeEmitCodeOperationContext context) {
        computeNode = node;
        graph = context.graph;
        program = context.program;
        debugState = context.debugState;
        createPipelinesFunction = context.createPipelinesFunction;
        dependentComputeNodes = context.dependentComputeNodes;
        dependentComputeInputs = context.dependentComputeInputs;
      }

      // TODO: Rename all!!
      public readonly ComputeNode computeNode;
      public readonly NanoGraph graph;
      public readonly NanoProgram program;
      public readonly DebugState debugState;

      public NanoFunction func;
      public NanoFunction validateCacheFunction { get; private set; }
      public TypeSpec resultTypeSpec { get; private set; }
      public NanoProgramType resultType { get; private set; }
      public DataSpec computeOutputSpec { get; private set; }

      public readonly NanoFunction createPipelinesFunction;

      public readonly IReadOnlyList<ComputeNodeResultEntry> dependentComputeNodes;
      public readonly IReadOnlyList<DataPlug> dependentComputeInputs;

      public IReadOnlyDictionary<DataPlug, CodeLocal> resultLocalMap { get; private set; }
      public SortedDictionary<string, (DataPlug, CodeLocal)> SortedResutLocalMap {
        get {
          SortedDictionary<string, (DataPlug, CodeLocal)> orderedResultLocalMap = new SortedDictionary<string, (DataPlug, CodeLocal)>();
          foreach (var entry in resultLocalMap) {
            orderedResultLocalMap.Add($"{entry.Key.Node.DebugId}.{entry.Key.FieldName}", (entry.Key, entry.Value));
          }
          return orderedResultLocalMap;
        }
      }

      public Dictionary<IComputeNode, List<(DataField field, int inputIndex)>> descendantInputs = new Dictionary<IComputeNode, List<(DataField field, int inputIndex)>>();

      public CodeLocal cachedResult { get; private set; }

      private string _lastUpdateFrameNumberIdentifier;
      private string _didInitializeFlagIdentifier;

      protected virtual IEnumerable<DataPlug> DependentComputeInputsToLoad => dependentComputeInputs;

      public void EmitFunctionSignature() {
        // Define a type to hold the result value.
        DataSpec computeInputSpec = computeNode.ComputeInputSpec;
        this.computeOutputSpec = computeNode.ComputeOutputSpec;
        TypeDecl resultTypeDecl = TypeDeclFromDataFields(computeOutputSpec.Fields);
        // DataField[] compileTimeOnlyInputs = CompileTimeOnlyFields(computeInputSpec.Fields);
        this.resultTypeSpec = TypeSpec.MakeType(resultTypeDecl);
        this.resultType = program.AddType(resultTypeDecl, $"Result_{computeNode.ShortName}");
        // Define a field to hold the cached result.
        string cachedResultIdentifier = program.AddInstanceField(resultType, $"Result_{computeNode.ShortName}");
        this.cachedResult = new CodeLocal { Type = resultTypeSpec, Identifier = cachedResultIdentifier };

        if (NeedsLastUpdateFrameNumber) {
          this._lastUpdateFrameNumberIdentifier = program.AddInstanceField(program.IntType, "LastFrameNumber");
          createPipelinesFunction.AddStatement($"{_lastUpdateFrameNumberIdentifier} = {createPipelinesFunction.EmitLiteral(-1)};");
        }
        if (NeedsDidInitializeFlag) {
          this._didInitializeFlagIdentifier = program.AddInstanceField(program.BoolType, "DidInitialize");
          createPipelinesFunction.AddStatement($"{_didInitializeFlagIdentifier} = {createPipelinesFunction.EmitLiteral(false)};");
        }
      }

      public abstract void EmitFunctionPreamble(out NanoFunction func);

      public void EmitLoadFunctionInputs() {
        int inputIndex = 0;
        foreach (var input in CollectComputeInputs(DependentComputeInputsToLoad)) {
          input.Operation.RecordLoadInputForDescendantNode(computeNode, input.Field, inputIndex++);
        }
      }

      public void ConsumeFunctionBodyResult(IReadOnlyDictionary<DataPlug, CodeLocal> resultLocalMap) {
        this.resultLocalMap = resultLocalMap;
      }
      public abstract void EmitFunctionReturn(out CodeCachedResult? result);

      public virtual void EmitValidateCacheFunction(NanoFunction validateCacheFunction) {
        this.validateCacheFunction = validateCacheFunction;
        switch (computeNode.ExecutionMode) {
          case ExecutionMode.OncePerFrame:
          case ExecutionMode.OncePerFrameAlways: {
            string frameNumberLocal = validateCacheFunction.AllocLocal("FrameNumber");
            validateCacheFunction.AddStatement($"{NanoProgram.IntIdentifier} {frameNumberLocal} = GetFrameNumber();");
            validateCacheFunction.AddStatement($"if ({_didInitializeFlagIdentifier} && {_lastUpdateFrameNumberIdentifier} == {frameNumberLocal}) {{");
            validateCacheFunction.AddStatement($"  return;");
            validateCacheFunction.AddStatement($"}}");
            validateCacheFunction.AddStatement($"{_didInitializeFlagIdentifier} = {validateCacheFunction.EmitLiteral(true)};");
            validateCacheFunction.AddStatement($"{_lastUpdateFrameNumberIdentifier} = {frameNumberLocal};");
            break;
          }
          case ExecutionMode.Once:
          case ExecutionMode.OnceOnStart:
            validateCacheFunction.AddStatement($"if ({_didInitializeFlagIdentifier}) {{");
            validateCacheFunction.AddStatement($"  return;");
            validateCacheFunction.AddStatement($"}}");
            validateCacheFunction.AddStatement($"{_didInitializeFlagIdentifier} = {validateCacheFunction.EmitLiteral(true)};");
            break;
          case ExecutionMode.OnTrigger:
          case ExecutionMode.OnDemand:
            break;
        }
        EmitValidateCacheFunctionInner();
      }

      public abstract void EmitValidateCacheFunctionInner();

      public bool NeedsLastUpdateFrameNumber =>
          computeNode.ExecutionMode == ExecutionMode.OncePerFrame ||
          computeNode.ExecutionMode == ExecutionMode.OncePerFrameAlways;
      public bool NeedsDidInitializeFlag =>
          computeNode.ExecutionMode == ExecutionMode.OncePerFrame ||
          computeNode.ExecutionMode == ExecutionMode.OncePerFrameAlways ||
          computeNode.ExecutionMode == ExecutionMode.Once ||
          computeNode.ExecutionMode == ExecutionMode.OnceOnStart;

      public void EmitExecuteFunctionCode(NanoFunction inFunction, ExecuteFunctionContextType contextType) {
        switch (contextType) {
          case ExecuteFunctionContextType.GlobalFrameStep:
            if (computeNode.ExecutionMode == ExecutionMode.OncePerFrameAlways ||
                computeNode.ExecutionMode == ExecutionMode.OnceOnStart) {
              inFunction.AddStatement($"{validateCacheFunction.Identifier}();");
            }
            break;
          case ExecuteFunctionContextType.OnDemand:
          default:
            if (inFunction.Context is NanoCpuContext) {
              inFunction.AddStatement($"{validateCacheFunction.Identifier}();");
            }
            break;
        }
      }

      public void RecordLoadInputForDescendantNode(IComputeNode descendant, DataField field, int inputIndex) {
        if (!descendantInputs.TryGetValue(descendant, out var result)) {
          result = new List<(DataField field, int inputIndex)>();
          descendantInputs[descendant] = result;
        }
        result.Add((field, inputIndex));
      }

      public DataField[] GetInputsForDescendantNode(IComputeNode descendant) {
        if (!descendantInputs.TryGetValue(descendant, out var result)) {
          return Array.Empty<DataField>();
        }
        return result.Select(entry => entry.field).ToArray();
      }

      public void EmitLoadInputsForDescendantNode(IComputeNode descendant, CodeContext context) {
        if (!descendantInputs.TryGetValue(descendant, out var inputs)) {
          return;
        }
        EmitExecuteFunctionCode(context.Function, ExecuteFunctionContextType.OnDemand);

        int index = 0;
        foreach (var input in inputs) {
          var fieldName = input.field.Name;
          var inputIndex = input.inputIndex;
          var intoLocal = context.OutputLocals[index++];
          EmitLoadOutput(context, fieldName, inputIndex, intoLocal);
        }
      }
 
      public void EmitValidateInputsForDescendantNode(IComputeNode descendant, NanoFunction validateCacheFunction, NanoFunction originalFunction) {
        if (!descendantInputs.TryGetValue(descendant, out var inputs)) {
          return;
        }
        if (originalFunction.Context is NanoGpuContext) {
          EmitExecuteFunctionCode(validateCacheFunction, ExecuteFunctionContextType.OnDemand);
        }
      }

      protected virtual void EmitLoadOutput(CodeContext context, string fieldName, int inputIndex, CodeLocal intoLocal) {
        string outputLocal = intoLocal.Identifier;
        // TODO: Unwind this. Caller can provide expression.
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(intoLocal.Type)} {outputLocal} = {context.Function.Context.EmitFunctionInput(program, cachedResult, fieldName, inputIndex)};");
      }

      private static TypeDecl TypeDeclFromDataFields(IEnumerable<DataField> fields) {
        return new TypeDecl(fields.Where(field => !field.IsCompileTimeOnly).Select(field => new TypeField { Name = field.Name, Type = field.Type, Attributes = field.Attributes }).ToArray());
      }


      protected struct ComputeInput {
        public DataPlug Plug;
        public DataField Field;
        public NanoProgramType FieldType;
        public IComputeNodeEmitCodeOperation Operation;
        public string Expression;
        public bool ReadWrite;
      }

      protected IEnumerable<ComputeInput> CollectComputeInputs(IEnumerable<DataPlug> inputs) {
        foreach (DataPlug input in inputs) {
          var resultOrNull = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == input.Node);
          if (resultOrNull == null) {
            NanoGraph.CurrentGenerateState.AddError($"Dependency {input.Node} for {computeNode} not yet ready.");
            continue;
          }
          ComputeNodeResultEntry result = resultOrNull.Value;
          string resultIdentifier = result.Result?.Result.Identifier;
          DataField? outputFieldOrNull = result.Node.ComputeOutputSpec.Fields.FirstOrNull(field => field.Name == input.FieldName);
          if (outputFieldOrNull == null) {
            NanoGraph.CurrentGenerateState.AddError($"Dependency {input.Node} for {computeNode} does not have output field {input.FieldName}.");
            continue;
          }

          // TODO: Figure out how to do this...
          bool readWrite = result.Node is ISplitComputeNode;

          DataField field = outputFieldOrNull.Value;
          if (field.IsCompileTimeOnly) {
            continue;
          }
          var fieldType = program.GetProgramType(field.Type, field.Name);
          yield return new ComputeInput {
            Plug = input,
            Field = field,
            FieldType = fieldType,
            Operation = result.Operation,
            Expression = $"{resultIdentifier}.{result.Result?.ResultType.GetField(field.Name)}",
            ReadWrite = readWrite,
          };
        }
      }


      protected static void AddGpuFuncInputs(NanoFunction func, IEnumerable<ComputeInput> inputs, List<NanoGpuBufferRef> gpuInputBuffers, ref int bufferIndex) {
        int inputIndex = 0;
        foreach (var computeInput in inputs) {
          AddGpuFuncInput(func, computeInput, $"input{inputIndex}", gpuInputBuffers, ref bufferIndex);
          inputIndex++;
        }
      }

      protected void AddDebugGpuFuncInputs(NanoFunction func, List<NanoGpuBufferRef> gpuInputBuffers, ref int bufferIndex) {
        if (!graph.DebugEnabled) {
          return;
        }
        AddGpuFuncInput(func, "debugState", debugState.DebugGpuStateType, debugState.DebugGpuStateIdentifier, "debugState", gpuInputBuffers, ref bufferIndex, isReadWrite: false);
      }

      protected static void AddGpuFuncInput(NanoFunction func, ComputeInput input, string paramName, List<NanoGpuBufferRef> gpuInputBuffers, ref int bufferIndex) {
        AddGpuFuncInput(func, input.Field, input.Expression, paramName, gpuInputBuffers, ref bufferIndex, isReadWrite: input.ReadWrite);
      }

      protected static void AddGpuFuncInput(NanoFunction func, DataField field, string inputExpression, string paramName, List<NanoGpuBufferRef> gpuInputBuffers, ref int bufferIndex, bool isReadWrite = false) {
        var fieldType = func.Program.GetProgramType(field.Type);
        AddGpuFuncInput(func, field.Name, fieldType, inputExpression, paramName, gpuInputBuffers, ref bufferIndex, isReadWrite: isReadWrite);
      }

      protected static void AddGpuFuncInput(NanoFunction func, string fieldName, NanoProgramType fieldType, string inputExpression, string paramName, List<NanoGpuBufferRef> gpuInputBuffers, ref int bufferIndex, bool isReadWrite = false) {
        gpuInputBuffers.Add(new NanoGpuBufferRef {
          FieldName = fieldName,
          Expression = inputExpression,
          ParameterName = paramName,
          Index = bufferIndex,
          Type = fieldType,
        });
        string[] modifiers = { "constant", "const" };
        string suffix = $"[[buffer({bufferIndex})]]";
        bool isReference = true;
        if (fieldType.IsArray) {
          modifiers = Array.Empty<string>();
          isReference = false;
        } else if (fieldType == func.Program.Texture) {
          suffix = $"[[texture({bufferIndex})]]";
          modifiers = Array.Empty<string>();
          isReference = false;
        }
        func.AddParam(modifiers, fieldType, paramName, suffix, new NanoParameterOptions { IsConst = !isReadWrite, IsReference = isReference });
        bufferIndex++;
      }

      protected static void AddGpuFuncOutputs(NanoFunction func, IEnumerable<DataField> fields, List<NanoGpuBufferRef> gpuOutputBuffers, ref int bufferIndex) {
        int index = 0;
        foreach (DataField field in fields) {
          if (field.IsCompileTimeOnly) {
            continue;
          }
          AddGpuFuncOutput(func, field, $"output{index}", gpuOutputBuffers, ref bufferIndex);
          index++;
        }
      }
      protected static void AddGpuFuncOutput(NanoFunction func, DataField field, string paramName, List<NanoGpuBufferRef> gpuOutputBuffers, ref int bufferIndex) {
        var fieldType = field.Type;
        NanoProgramType programType = func.Program.GetProgramType(fieldType, field.Name);
        AddGpuFuncOutput(func, field.Name, programType, paramName, expression: null, gpuOutputBuffers, ref bufferIndex);
      }

      protected static void AddGpuFuncOutput(NanoFunction func, string fieldName, NanoProgramType fieldType, string paramName, string expression, List<NanoGpuBufferRef> gpuOutputBuffers, ref int bufferIndex) {
        gpuOutputBuffers.Add(new NanoGpuBufferRef {
          FieldName = fieldName,
          Expression = expression,
          ParameterName = paramName,
          Index = bufferIndex,
          Type = fieldType,
        });
        string[] modifiers = {};
        string suffix = $"[[buffer({bufferIndex})]]";
        NanoProgramType programType = fieldType;
        if (fieldType.IsArray) {
          modifiers = Array.Empty<string>();
        } else if (fieldType == func.Program.Texture) {
          suffix = $"[[texture({bufferIndex})]]";
          modifiers = Array.Empty<string>();
          programType = func.Program.WriteTexture;
        }
        func.AddParam(modifiers, programType, paramName, suffix, new NanoParameterOptions { IsConst = false });
        bufferIndex++;
      }

      protected static void EmitSyncBuffersToGpu(NanoFunction func, IReadOnlyList<NanoGpuBufferRef> gpuInputBuffers) {
        foreach (var inputBuffer in gpuInputBuffers) {
          if (inputBuffer.Type.IsArray) {
            func.AddStatement($"{inputBuffer.Expression}->SyncToGpu();");
          }
        }
      }

      protected static void EmitSyncBuffersToGpu(NanoFunction func, CodeLocal output, IReadOnlyList<NanoGpuBufferRef> gpuInputBuffers, IReadOnlyList<NanoGpuBufferRef> gpuOutputBuffers) {
        EmitSyncBuffersToGpu(func, gpuInputBuffers);
        NanoProgramType outputType = func.Program.GetProgramType(output.Type);
        foreach (var outputBuffer in gpuOutputBuffers) {
          var fieldName = outputType.GetField(outputBuffer.FieldName);
          if (outputBuffer.Type.IsArray) {
            func.AddStatement($"{output.Identifier}.{fieldName}->EnsureGpuBuffer();");
          }
        }
      }

      protected static void EmitBindBuffers(NanoFunction func, IReadOnlyList<NanoGpuBufferRef> gpuInputBuffers, string variant = null) {
        variant = variant ?? "";
        foreach (var inputBuffer in gpuInputBuffers) {
          string expression = inputBuffer.Expression;
          int bufferIndex = inputBuffer.Index;
          if (inputBuffer.Type.IsArray) {
            func.AddStatement($"[encoder set{variant}Buffer:{expression}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
          } else if (inputBuffer.Type == func.Program.Texture) {
            func.AddStatement($"[encoder set{variant}Texture:{expression} atIndex:{bufferIndex}];");
          } else {
            func.AddStatement($"[encoder set{variant}Bytes:&{expression} length:sizeof({expression}) atIndex:{bufferIndex}];");
          }
        }
      }

      protected static void EmitBindBuffers(NanoFunction func, CodeLocal output, IReadOnlyList<NanoGpuBufferRef> gpuInputBuffers, IReadOnlyList<NanoGpuBufferRef> gpuOutputBuffers, string variant = null) {
        variant = variant ?? "";
        EmitBindBuffers(func, gpuInputBuffers, variant: variant);
        NanoProgramType outputType = func.Program.GetProgramType(output.Type);
        foreach (var outputBuffer in gpuOutputBuffers) {
          string accessExpr;
          if (outputBuffer.Expression != null) {
            accessExpr = outputBuffer.Expression;
          } else {
            var fieldName = outputType.GetField(outputBuffer.FieldName);
            accessExpr = $"{output.Identifier}.{fieldName}";
          }
          int bufferIndex = outputBuffer.Index;
          if (outputBuffer.Type.IsArray) {
            func.AddStatement($"[encoder set{variant}Buffer:{accessExpr}->GetGpuBuffer() offset:0 atIndex:{bufferIndex}];");
          } else if (outputBuffer.Type == func.Program.Texture) {
            func.AddStatement($"[encoder set{variant}Texture:{accessExpr} atIndex:{bufferIndex}];");
          }
        }
      }

      protected static void EmitMarkBuffersDirty(NanoFunction func, CodeLocal output, IReadOnlyList<NanoGpuBufferRef> gpuInputBuffers, IReadOnlyList<NanoGpuBufferRef> gpuOutputBuffers) {
        NanoProgramType outputType = func.Program.GetProgramType(output.Type);
        foreach (var outputBuffer in gpuOutputBuffers) {
          var fieldName = outputType.GetField(outputBuffer.FieldName);
          if (outputBuffer.Type.IsArray) {
            func.AddStatement($"{output.Identifier}.{fieldName}->MarkGpuBufferChanged();");
          }
        }
      }
    }
  }
}

