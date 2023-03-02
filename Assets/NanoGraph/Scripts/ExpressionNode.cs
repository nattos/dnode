using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public enum ExpressionSource {
    InlineBlock,
    InlineExpression,
    Resource,
  }

  public interface IResourceDepsNode {
    string[] DependentAssetPaths { get; }
  }

  public class ExpressionNode : DataNode, ICodeNode, IResourceDepsNode {
    [EditableAttribute]
    public ExpressionSource Source;
    [EditableAttribute]
    public string InlineExpression;
    [EditableAttribute]
    public TextAsset Resource;

    protected override string ShortNamePart => Source == ExpressionSource.InlineExpression ? "InlineExpression" : $"{Source}Expression";

    [EditableAttribute]
    public TypeDeclBuilder InputFields = new TypeDeclBuilder();
    [EditableAttribute]
    public TypeDeclBuilder OutputFields = new TypeDeclBuilder();

    public override DataSpec InputSpec => DataSpec.FromTypeFields(InputFields?.AsTypeFields() ?? Array.Empty<TypeField>());
    public override DataSpec OutputSpec => DataSpec.FromTypeFields(OutputFields?.AsTypeFields() ?? Array.Empty<TypeField>());

    public string[] DependentAssetPaths {
      get {
        switch (Source) {
          default:
          case ExpressionSource.InlineBlock:
          case ExpressionSource.InlineExpression:
            return Array.Empty<string>();
          case ExpressionSource.Resource:
            if (Resource == null) {
              return null;
            }
            return new[] { UnityEditor.AssetDatabase.GetAssetPath(Resource) };
        }
      }
    }

    public string SourceExpr {
      get {
        switch (Source) {
          default:
          case ExpressionSource.InlineBlock:
          case ExpressionSource.InlineExpression:
            return InlineExpression;
          case ExpressionSource.Resource:
            return Resource == null ? null : System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), UnityEditor.AssetDatabase.GetAssetPath(Resource)));
        }
      }
    }

    public void EmitCode(CodeContext context) {
      int inputCount = Mathf.Min(context.InputLocals.Count, InputFields.Fields.Count);
      int outputCount = Mathf.Min(context.OutputLocals.Count, OutputFields.Fields.Count);
      for (int i = 0; i < outputCount; ++i) {
        var outputLocal = context.OutputLocals[i];
        context.Function.AddStatement($"{context.Function.GetTypeIdentifier(outputLocal.Type)} {outputLocal.Identifier};");
      }
      context.Function.AddStatement($"{{");
      for (int i = 0; i < inputCount; ++i) {
        var inputLocal = context.InputLocals[i];
        var inputField = InputFields.Fields[i];
        context.Function.AddStatement($"  {context.Function.GetTypeIdentifier(inputField.AsTypeSpec())} {NanoProgram.SanitizeIdentifierFragment(inputField.Name)} = {inputLocal.Identifier};");
      }
      if (Source == ExpressionSource.InlineExpression) {
        if (outputCount != 1) {
          NanoGraph.CurrentGenerateState.AddError($"When using {ExpressionSource.InlineExpression} the expression must have exactly one output.");
        } else {
          context.Function.AddStatement($"  {context.OutputLocals[0].Identifier} = {SourceExpr ?? ""};");
        }
      } else {
        for (int i = 0; i < outputCount; ++i) {
          var outputField = OutputFields.Fields[i];
          context.Function.AddStatement($"  {context.Function.GetTypeIdentifier(outputField.AsTypeSpec())} {NanoProgram.SanitizeIdentifierFragment(outputField.Name)};");
        }
        context.Function.AddStatement(SourceExpr ?? "");
        for (int i = 0; i < outputCount; ++i) {
          var outputLocal = context.OutputLocals[i];
          var outputField = OutputFields.Fields[i];
          context.Function.AddStatement($"  {outputLocal.Identifier} = {NanoProgram.SanitizeIdentifierFragment(outputField.Name)};");
        }
      }
      context.Function.AddStatement($"}}");
    }
  }
}
