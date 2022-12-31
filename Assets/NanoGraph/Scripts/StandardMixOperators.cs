using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public enum StandardMixType {
    Lerp,
    Blend,
    Add,
    Subtract,
  }

  public struct StandardMixCodeEmitter {
    public string FunctionIdentifier;
    public string OuterFunctionIdentifier;

    public static StandardMixCodeEmitter MakeChainedFunction(string identifier, string outerFuncIdentifier = null) {
      return new StandardMixCodeEmitter { FunctionIdentifier = identifier, OuterFunctionIdentifier = outerFuncIdentifier };
    }
  }

  public struct StandardMixOptions {
    public bool UseAlpha;
  }

  public class StandardMixer {
    public readonly StandardMixType Type;
    public readonly StandardMixCodeEmitter CodeEmitter;

    public StandardMixer(StandardMixType type, StandardMixCodeEmitter codeEmitter) {
      Type = type;
      CodeEmitter = codeEmitter;
    }

    public string EmitExpressionCode(CodeContext context, IReadOnlyList<(string inputExpr, string alphaExpr)> inputExpressions, StandardMixOptions options) {
      string result = null;
      string useAlphaExpr = context.Function.EmitLiteral(options.UseAlpha);
      foreach ((string inputExpr, string alphaExpr) in inputExpressions) {
        if (result == null) {
          result = $"{CodeEmitter.FunctionIdentifier}<{useAlphaExpr}>({inputExpr}, {alphaExpr})";
        } else {
          result = $"{CodeEmitter.FunctionIdentifier}<{useAlphaExpr}>({result}, {inputExpr}, {alphaExpr})";
        }
      }
      if (!string.IsNullOrEmpty(CodeEmitter.OuterFunctionIdentifier)) {
        result = $"{CodeEmitter.OuterFunctionIdentifier}({result})";
      }
      return result;
    }
  }

  public static class StandardMixers {
    public static readonly IReadOnlyList<StandardMixer> All;
    public static readonly IReadOnlyDictionary<StandardMixType, StandardMixer> ByType;

    public static StandardMixer Get(StandardMixType type) => ByType[type];

    static StandardMixers() {
      All = new[] {
          new StandardMixer(StandardMixType.Lerp, codeEmitter: StandardMixCodeEmitter.MakeChainedFunction("lerp_mix")),
          new StandardMixer(StandardMixType.Blend, codeEmitter: StandardMixCodeEmitter.MakeChainedFunction("blend_mix")),
          new StandardMixer(StandardMixType.Add, codeEmitter: StandardMixCodeEmitter.MakeChainedFunction("add_mix")),
          new StandardMixer(StandardMixType.Subtract, codeEmitter: StandardMixCodeEmitter.MakeChainedFunction("subtract_mix")),
      };
      ByType = All.ToDictionary(op => op.Type, op => op);
    }
  }
}
