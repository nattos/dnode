using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NanoGraph {
  public enum ExecutionMode {
    // Cache and update at most once a frame.
    OncePerFrame,
    // Execute and cache first time requested.
    Once,
    // Execute and cache at program start.
    OnceOnStart,
    // Only update when trigger signal sent, or first time requested.
    OnTrigger,
    // Do not cache. Executes everytime value is polled.
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

    public virtual DataSpec ComputeInputSpec => DataSpec.ExtendWithFields(GetInputOutputDataSpec(FieldPortsMode.Combined, InputTypeFields), TypeDeclFields);
    public virtual DataSpec AuxSizesOutputSpec => DataSpec.Empty;
    public override DataSpec InputSpec => DataSpec.ExtendWithFields(GetInputOutputDataSpec(InputPortsMode, InputTypeFields), TypeDeclFields);
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

    protected DataField[] TypeDeclFields => new[] { new DataField { Name = "TypeDecl", IsCompileTimeOnly = true, Type = TypeSpec.MakePrimitive(PrimitiveType.TypeDecl) } };

    public virtual TypeField[] InputTypeFields => InputOutputTypeFields;
    public virtual TypeField[] OutputTypeFields => InputOutputTypeFields;

    public virtual TypeField[] InputOutputTypeFields {
      get {
        switch (TypeDeclMode) {
          case TypeDeclMode.Internal:
          default:
            return InternalType.Fields.ToArray();
          case TypeDeclMode.External: {
            TypeSpec? field = Graph.GetEdgeToDestinationOrNull(new DataPlug { Node = this, FieldName = "TypeDecl" })?.SourceFieldOrNull?.Type;
            TypeDecl inputType = field?.Type ?? TypeDecl.Empty;
            return inputType.Fields.ToArray();
          }
        }
      }
    }

    public abstract INanoCodeContext CodeContext { get; }

    public virtual void EmitStoreAuxSizesCode(CodeContext context, CodeCachedResult cachedResult) {
    }

    public abstract string EmitTotalThreadCount(NanoFunction func, CodeCachedResult cachedResult);

    public abstract IComputeNodeEmitCodeOperation CreateEmitCodeOperation(ComputeNodeEmitCodeOperationContext context);





    public abstract class EmitterBase : IComputeNodeEmitCodeOperation {
      public EmitterBase(ComputeNode node, ComputeNodeEmitCodeOperationContext context) {
        computeNode = node;
        errors = context.errors;
        graph = context.graph;
        program = context.program;
        executeFunction = context.executeFunction;
        createPipelinesFunction = context.createPipelinesFunction;
        dependentComputeNodes = context.dependentComputeNodes;
        dependentComputeInputs = context.dependentComputeInputs;
      }

      // TODO: Rename all!!
      public readonly List<string> errors;
      public readonly ComputeNode computeNode;
      public readonly NanoGraph graph;
      public readonly NanoProgram program;

      public NanoFunction func;
      public NanoFunction arraySizesFunc;
      public NanoFunction validateSizesCacheFunction;
      public NanoFunction validateCacheFunction;
      public TypeSpec resultTypeSpec { get; private set; }
      public NanoProgramType resultType { get; private set; }
      public NanoProgramType arraySizeResultType { get; private set; }
      public DataSpec computeOutputSpec { get; private set; }

      public readonly NanoFunction executeFunction;
      public readonly NanoFunction createPipelinesFunction;

      public readonly IReadOnlyList<ComputeNodeResultEntry> dependentComputeNodes;
      public readonly IReadOnlyList<DataPlug> dependentComputeInputs;

      public IReadOnlyDictionary<DataPlug, CodeLocal> resultLocalMap { get; private set; }

      public Dictionary<IComputeNode, List<(DataField field, int inputIndex)>> descendantInputs = new Dictionary<IComputeNode, List<(DataField field, int inputIndex)>>();

      public CodeLocal cachedResult { get; private set; }
      // public string cachedResultIdentifier;
      // public string cachedResultSizesIdentifier;

      protected virtual IEnumerable<DataPlug> DependentComputeInputsToLoad => dependentComputeInputs;

      public void EmitFunctionSignature() {
        // Define a type to hold the result value.
        DataSpec computeInputSpec = computeNode.ComputeInputSpec;
        DataSpec computeArrayInputSpec = computeNode.AuxSizesOutputSpec;
        this.computeOutputSpec = computeNode.OutputSpec;
        TypeDecl resultTypeDecl = TypeDeclFromDataFields(computeOutputSpec.Fields);
        // DataField[] compileTimeOnlyInputs = CompileTimeOnlyFields(computeInputSpec.Fields);
        this.resultTypeSpec = TypeSpec.MakeType(resultTypeDecl);
        this.resultType = program.AddType(resultTypeDecl, $"Result_{computeNode.ShortName}");
        this.arraySizeResultType = program.AddType(TypeDeclFromDataFields(computeOutputSpec.Fields.Concat(computeArrayInputSpec.Fields).Select(field => new DataField { Name = field.Name, Type = TypeSpec.MakePrimitive(PrimitiveType.Int), IsCompileTimeOnly = field.IsCompileTimeOnly }).ToArray()), $"ResultSizes_{computeNode.ShortName}");
        // Define a field to hold the cached result.
        string cachedResultIdentifier = program.AddInstanceField(resultType, $"Result_{computeNode.ShortName}");
        string cachedResultSizesIdentifier = program.AddInstanceField(arraySizeResultType, $"ResultSizes_{computeNode.ShortName}");
        this.cachedResult = new CodeLocal { Type = resultTypeSpec, Identifier = cachedResultIdentifier, ArraySizeIdentifier = cachedResultSizesIdentifier };
      }

      public abstract void EmitFunctionPreamble(out NanoFunction func, out NanoFunction arraySizesFunc);

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

      public void EmitValidateSizesCacheFunction() {
        validateSizesCacheFunction = program.AddFunction($"UpdateSizes_{computeNode.ShortName}", NanoProgram.CpuContext, Array.Empty<NanoProgramType>(), program.VoidType);
        validateSizesCacheFunction.AddStatement($"{cachedResult.ArraySizeIdentifier} = {arraySizesFunc.Identifier}();");
        foreach (var field in computeNode.OutputSpec.Fields) {
          if (field.IsCompileTimeOnly) {
            continue;
          }
          if (!field.Type.IsArray) {
            continue;
          }
          validateSizesCacheFunction.AddStatement($"if (!{cachedResult.Identifier}.{resultType.GetField(field.Name)}) {{");
          validateSizesCacheFunction.AddStatement($"  {cachedResult.Identifier}.{resultType.GetField(field.Name)}.reset(NanoTypedBuffer<{validateSizesCacheFunction.GetElementTypeIdentifier(field.Type)}>::Allocate({cachedResult.ArraySizeIdentifier}.{arraySizeResultType.GetField(field.Name)}));");
          validateSizesCacheFunction.AddStatement($"}} else {{");
          validateSizesCacheFunction.AddStatement($"  {cachedResult.Identifier}.{resultType.GetField(field.Name)}->Resize({cachedResult.ArraySizeIdentifier}.{arraySizeResultType.GetField(field.Name)});");
          validateSizesCacheFunction.AddStatement($"}}");
        }
      }

      public abstract void EmitValidateCacheFunction();

      public void EmitExecuteFunctionCode() {
        executeFunction.AddStatement($"{validateCacheFunction.Identifier}();");
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

        int index = 0;
        foreach (var input in inputs) {
          var fieldName = input.field.Name;
          var inputIndex = input.inputIndex;
          var intoLocal = context.OutputLocals[index++];
          EmitLoadOutput(context, fieldName, inputIndex, intoLocal);
        }
      }

      protected virtual void EmitLoadOutput(CodeContext context, string fieldName, int inputIndex, CodeLocal intoLocal) {
        string outputLocal = intoLocal.Identifier;
        string outputSizeLocal = intoLocal.ArraySizeIdentifier;
        // TODO: Unwind this. Caller can provide expression.
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(intoLocal.Type)} {outputLocal} = {context.Function.Context.EmitFunctionInput(program, cachedResult, fieldName, inputIndex)};");
        context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {outputSizeLocal} = {cachedResult.ArraySizeIdentifier}.{arraySizeResultType.GetField(fieldName)};");
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
      }

      protected IEnumerable<ComputeInput> CollectComputeInputs(IEnumerable<DataPlug> inputs) {
        foreach (DataPlug input in inputs) {
          var resultOrNull = dependentComputeNodes.FirstOrNull(dependency => dependency.Node == input.Node);
          if (resultOrNull == null) {
            errors.Add($"Dependency {input.Node} for {computeNode} not yet ready.");
            continue;
          }
          ComputeNodeResultEntry result = resultOrNull.Value;
          string resultIdentifier = result.Result?.Result.Identifier;
          DataField? outputFieldOrNull = input.Node.OutputSpec.Fields.FirstOrNull(field => field.Name == input.FieldName);
          if (outputFieldOrNull == null) {
            errors.Add($"Dependency {input.Node} for {computeNode} does not have output field {input.FieldName}.");
            continue;
          }
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
          };
        }
      }
    }
  }
}

