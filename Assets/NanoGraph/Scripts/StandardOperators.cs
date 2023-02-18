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
    Modulo,

    Max,
    Min,
  
    Pow,
    Log,
    Atan2,

    Abs,
    Saturate,
    Negate,
    Invert,
    OneMinus,
    Sign,
    Floor,
    Ceil,
    Round,
    Truncate,
    Fraction,
    Magnitude,
    Normalize,
    Cos,
    Sin,
    Exp,
    Pow2,
    Pow10,
    LogE,
    Log2,
    Log10,
    Atan,

    Not,
    And,
    Or,
    Xor,
    Greater,
    Less,
    GreaterOrEqual,
    LessOrEqual,

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
    public string OuterFunctionIdentifier;
    public FunctionMultiInputStyle FunctionMultiInputStyle;

    public static StandardOperatorCodeEmitter MakeOperator(string symbol) {
      return new StandardOperatorCodeEmitter { OperatorSymbol = symbol };
    }

    public static StandardOperatorCodeEmitter MakeChainedFunction(string identifier, string outerFuncIdentifier = null) {
      return new StandardOperatorCodeEmitter { FunctionIdentifier = identifier, OuterFunctionIdentifier = outerFuncIdentifier, FunctionMultiInputStyle = FunctionMultiInputStyle.Chained };
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
          string result = null;
          if (inputExpressions.Count == 1) {
            result = $"{CodeEmitter.FunctionIdentifier}({inputExpressions[0]})";
          } else {
            foreach (string exp in inputExpressions) {
              if (result == null) {
                result = exp;
              } else {
                result = $"{CodeEmitter.FunctionIdentifier}({result}, {exp})";
              }
            }
          }
          if (!string.IsNullOrEmpty(CodeEmitter.OuterFunctionIdentifier)) {
            result = $"{CodeEmitter.OuterFunctionIdentifier}({result})";
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
          new StandardOperator(StandardOperatorType.Modulo, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("modulo_op")),

          new StandardOperator(StandardOperatorType.Max, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("max_op")),
          new StandardOperator(StandardOperatorType.Min, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("min_op")),

          new StandardOperator(StandardOperatorType.Pow, inputCount: 2, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("pow_op")),
          new StandardOperator(StandardOperatorType.Log, inputCount: 2, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("log_op")),
          new StandardOperator(StandardOperatorType.Atan2, inputCount: 2, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("atan2_op")),

          new StandardOperator(StandardOperatorType.Abs, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("abs_op")),
          new StandardOperator(StandardOperatorType.Saturate, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("saturate")),
          new StandardOperator(StandardOperatorType.Negate, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("negate")),
          new StandardOperator(StandardOperatorType.Invert, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("invert")),
          new StandardOperator(StandardOperatorType.OneMinus, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("one_minus")),
          new StandardOperator(StandardOperatorType.Sign, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("sign_op")),
          new StandardOperator(StandardOperatorType.Floor, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("floor_op")),
          new StandardOperator(StandardOperatorType.Ceil, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("ceil_op")),
          new StandardOperator(StandardOperatorType.Round, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("round_op")),
          new StandardOperator(StandardOperatorType.Truncate, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("truncate_op")),
          new StandardOperator(StandardOperatorType.Fraction, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("fraction_op")),
          new StandardOperator(StandardOperatorType.Magnitude, inputCount: 1, fixedOutputType: PrimitiveType.Float, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("magnitude_op")),
          new StandardOperator(StandardOperatorType.Normalize, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("normalize_op")),
          new StandardOperator(StandardOperatorType.Cos, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("cos_op")),
          new StandardOperator(StandardOperatorType.Sin, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("sin_op")),
          new StandardOperator(StandardOperatorType.Exp, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("exp_op")),
          new StandardOperator(StandardOperatorType.Pow2, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("pow2_op")),
          new StandardOperator(StandardOperatorType.Pow10, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("pow10_op")),
          new StandardOperator(StandardOperatorType.LogE, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("logE_op")),
          new StandardOperator(StandardOperatorType.Log2, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("log2_op")),
          new StandardOperator(StandardOperatorType.Log10, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("log10_op")),
          new StandardOperator(StandardOperatorType.Atan, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("atan_op")),

          new StandardOperator(StandardOperatorType.Not, inputCount: 1, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("not_op"), fixedOutputType: PrimitiveType.Bool),
          new StandardOperator(StandardOperatorType.And, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("and_op"), fixedOutputType: PrimitiveType.Bool),
          new StandardOperator(StandardOperatorType.Or, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("or_op"), fixedOutputType: PrimitiveType.Bool),
          new StandardOperator(StandardOperatorType.Xor, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("xor_op"), fixedOutputType: PrimitiveType.Bool),
          new StandardOperator(StandardOperatorType.Greater, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("greater_than_op", "to_bool"), fixedOutputType: PrimitiveType.Bool),
          new StandardOperator(StandardOperatorType.Less, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("less_than_op", "to_bool"), fixedOutputType: PrimitiveType.Bool),
          new StandardOperator(StandardOperatorType.GreaterOrEqual, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("greater_or_equal_op", "to_bool"), fixedOutputType: PrimitiveType.Bool),
          new StandardOperator(StandardOperatorType.LessOrEqual, codeEmitter: StandardOperatorCodeEmitter.MakeChainedFunction("less_or_equal_op", "to_bool"), fixedOutputType: PrimitiveType.Bool),
      };
      ByType = All.ToDictionary(op => op.Type, op => op);
    }
  }
}
