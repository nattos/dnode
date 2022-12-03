using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DNode;
using UnityEngine;

namespace NanoGraph {
  public enum StandardOperatorType {
    Add,
    Subtract,
    Multiply,
    Divide,

    Max,
    Min,
  
    Pow,
    Log,
    Atan2,

    Abs,
    Clamp,
    Negate,
    Invert,
    OneMinus,
    Sign,
    Cos,
    Sin,
    Exp,
    Pow2,
    Pow10,
    LogE,
    Log2,
    Log10,
    Atan,

    // Not,
    // And,
    // Or,
    // Xor,
    // Greater,
    // Less,
    // GreaterOrEqual,
    // LessOrEqual,

    // Concat,
    // Union,
    // Intersection,
  }

  public enum FunctionMultiInputStyle {
    Vararg,
    Chained,
  }

  public struct StandardOperatorCodeEmitter {
    public string OperatorSymbol;
    public string FunctionIdentifier;
    public FunctionMultiInputStyle FunctionMultiInputStyle;

    public static StandardOperatorCodeEmitter MakeOperator(string symbol) {
      return new StandardOperatorCodeEmitter { OperatorSymbol = symbol };
    }

    public static StandardOperatorCodeEmitter MakeChainedFunction(string identifier) {
      return new StandardOperatorCodeEmitter { FunctionIdentifier = identifier, FunctionMultiInputStyle = FunctionMultiInputStyle.Chained };
    }

    public static StandardOperatorCodeEmitter MakeVarargFunction(string identifier) {
      return new StandardOperatorCodeEmitter { FunctionIdentifier = identifier, FunctionMultiInputStyle = FunctionMultiInputStyle.Vararg };
    }
  }

  public class StandardOperator {
    public readonly StandardOperatorType Type;
    public readonly int? InputCount;
    public int FixedInputCount => InputCount ?? 0;
    public bool IsMultipleInput => InputCount == null;
    public readonly PrimitiveType? FixedOutputType;
    public readonly StandardOperatorCodeEmitter CodeEmitter;

    public StandardOperator(StandardOperatorType type, StandardOperatorCodeEmitter codeEmitter, int? inputCount = null, PrimitiveType? fixedOutputType = null) {
      Type = type;
      CodeEmitter = codeEmitter;
      InputCount = inputCount;
      FixedOutputType = fixedOutputType;
    }

    public string EmitExpressionCode(CodeContext context, IReadOnlyList<string> inputExpressions) {
      if (CodeEmitter.OperatorSymbol != null) {
        return string.Join($" {CodeEmitter.OperatorSymbol} ", inputExpressions);
      }
      switch (CodeEmitter.FunctionMultiInputStyle) {
        case FunctionMultiInputStyle.Vararg:
          return $"{CodeEmitter.FunctionIdentifier}({string.Join(", ", inputExpressions)})";
        case FunctionMultiInputStyle.Chained:
        default: {
          if (inputExpressions.Count == 1) {
            return $"{CodeEmitter.FunctionIdentifier}({inputExpressions[0]})";
          }
          string result = null;
          foreach (string exp in inputExpressions) {
            if (result == null) {
              result = exp;
            } else {
              result = $"{CodeEmitter.FunctionIdentifier}({result}, {exp})";
            }
          }
          return result;
        }
      }
    }
  }

  public static class StandardOperators {
    public static readonly IReadOnlyList<StandardOperator> All;
    public static readonly IReadOnlyDictionary<StandardOperatorType, StandardOperator> ByType;

    public static StandardOperator Get(StandardOperatorType type) => ByType[type];

    static StandardOperators() {
      All = new[] {
          new StandardOperator(StandardOperatorType.Add, codeEmitter: StandardOperatorCodeEmitter.MakeOperator("+")),
          new StandardOperator(StandardOperatorType.Subtract, codeEmitter: StandardOperatorCodeEmitter.MakeOperator("-")),
          new StandardOperator(StandardOperatorType.Multiply, codeEmitter: StandardOperatorCodeEmitter.MakeOperator("*")),
          new StandardOperator(StandardOperatorType.Divide, codeEmitter: StandardOperatorCodeEmitter.MakeOperator("/")),

          new StandardOperator(StandardOperatorType.Max, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("max")),
          new StandardOperator(StandardOperatorType.Min, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("min")),

          new StandardOperator(StandardOperatorType.Pow, inputCount: 2, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("pow")),
          new StandardOperator(StandardOperatorType.Log, inputCount: 2, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("log")),
          new StandardOperator(StandardOperatorType.Atan2, inputCount: 2, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("atan2")),

          new StandardOperator(StandardOperatorType.Abs, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("abs")),
          new StandardOperator(StandardOperatorType.Clamp, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("clamp01")),
          new StandardOperator(StandardOperatorType.Negate, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("negate")),
          new StandardOperator(StandardOperatorType.Invert, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("invert")),
          new StandardOperator(StandardOperatorType.OneMinus, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("one_minus")),
          new StandardOperator(StandardOperatorType.Sign, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("sign")),
          new StandardOperator(StandardOperatorType.Cos, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("cos")),
          new StandardOperator(StandardOperatorType.Sin, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("sin")),
          new StandardOperator(StandardOperatorType.Exp, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("exp")),
          new StandardOperator(StandardOperatorType.Pow2, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("pow2")),
          new StandardOperator(StandardOperatorType.Pow10, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("pow10")),
          new StandardOperator(StandardOperatorType.LogE, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("logE")),
          new StandardOperator(StandardOperatorType.Log2, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("log2")),
          new StandardOperator(StandardOperatorType.Log10, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("log10")),
          new StandardOperator(StandardOperatorType.Atan, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("atan")),
      };
      ByType = All.ToDictionary(op => op.Type, op => op);
    }
  }
}
