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

    private DataField[] TypeDeclFields => new[] { new DataField { Name = "TypeDecl", IsCompileTimeOnly = true, Type = TypeSpec.MakePrimitive(PrimitiveType.TypeDecl) } };

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

    public void EmitLoadOutputsCode(CodeContext context, CodeCachedResult cachedResult) {
      var outputSpec = OutputSpec;
      for (int i = 0; i < outputSpec.Fields.Count; ++i) {
        var field = outputSpec.Fields[i];
        string outputLocal = context.OutputLocals[i].Identifier;
        string outputSizeLocal = context.OutputLocals[i].ArraySizeIdentifier;
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(field.Type)} {outputLocal} = {context.Function.Context.EmitFunctionInput(cachedResult, field, i)};");
        context.ArraySizeFunction.AddStatement($"{NanoProgram.IntIdentifier} {outputLocal} = {cachedResult.Result.ArraySizeIdentifier}.{cachedResult.ArraySizesResultType.GetField(field.Name)};");
      }
    }

    public abstract string EmitTotalThreadCount(NanoFunction func, CodeCachedResult cachedResult);
  }

  public enum ThreadCountMode {
    Integer,
    ArraySize,
  }

  public enum TypeDeclMode {
    Internal,
    External,
  }

  public class VectorComputeNode : ComputeNode, IAutoTypeNode {
    // TODO: Support multiple buffer modes.
    public override INanoCodeContext CodeContext => NanoProgram.GpuContext;
    public override DataSpec AuxSizesOutputSpec => DataSpec.FromFields(ThreadCountFields);

    public override DataSpec ComputeInputSpec => base.ComputeInputSpec;
    public override DataSpec InputSpec => DataSpec.ExtendWithFields(base.InputSpec, ThreadCountFields);

    public override DataSpec OutputSpec {
      get {
        return DataSpec.FromFields(DataField.MakeType("Out", TypeSpec.MakeArray(TypeSpec.MakeType(new TypeDecl(OutputTypeFields.ToArray())))));
      }
    }

    public DataField[] ThreadCountFields {
      get {
        switch (ThreadCountMode) {
          case ThreadCountMode.Integer:
            return new[] { DataField.MakePrimitive("ThreadCount", PrimitiveType.Int) };
          case ThreadCountMode.ArraySize:
          default:
            return new[] { DataField.MakeType("ThreadCountFromArray", TypeSpec.MakeArray(ThreadCountFromArrayElementType)) };
        }
      }
    }

    [EditableAttribute]
    public ThreadCountMode ThreadCountMode = ThreadCountMode.Integer;
    [EditableAttribute]
    public AutoType ThreadCountFromArrayAutoType = AutoType.Auto;
    public TypeSpec ThreadCountFromArrayElementType;

    public void UpdateTypesFromInputs() {
      AutoTypeUtils.UpdateAutoType(Graph.GetEdgeToDestinationOrNull(this, "ThreadCountFromArray"), ref ThreadCountFromArrayElementType, forceIsArray: false);
    }

    public override void EmitStoreAuxSizesCode(CodeContext context, CodeCachedResult cachedResult) {
      string threadCountFieldName;
      string threadCountExpr;
      switch (ThreadCountMode) {
        case ThreadCountMode.Integer:
          threadCountFieldName = "ThreadCount";
          threadCountExpr = $"{context.InputLocals[0].Identifier}";
          break;
        case ThreadCountMode.ArraySize:
        default:
          threadCountFieldName = "ThreadCountFromArray";
          threadCountExpr = $"{context.InputLocals[0].ArraySizeIdentifier}";
          break;
      }
      context.ArraySizeFunction.AddStatement($"{cachedResult.Result.ArraySizeIdentifier}.{cachedResult.ArraySizesResultType.GetField(threadCountFieldName)} = {threadCountExpr};");
    }

    public override string EmitTotalThreadCount(NanoFunction func, CodeCachedResult cachedResult) {
      switch (ThreadCountMode) {
        case ThreadCountMode.Integer:
          return $"{cachedResult.Result.ArraySizeIdentifier}.{cachedResult.ArraySizesResultType.GetField("ThreadCount")}";
        case ThreadCountMode.ArraySize:
        default:
          return $"{cachedResult.Result.ArraySizeIdentifier}.{cachedResult.ArraySizesResultType.GetField("ThreadCountFromArray")}";
      }
    }
  }

  public class ScalarComputeNode : ComputeNode {
    public override INanoCodeContext CodeContext => NanoProgram.CpuContext;

    public override string EmitTotalThreadCount(NanoFunction func, CodeCachedResult cachedResult) {
      return func.EmitLiteral(1);
    }
  }
}

