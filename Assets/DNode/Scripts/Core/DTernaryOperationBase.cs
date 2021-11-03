using System;
using Unity.VisualScripting;

namespace DNode {
  public abstract class DTernaryOperationBase<TData> : Unit {
    [DoNotSerialize][PortLabelHidden] public ValueInput A;
    [DoNotSerialize][PortLabelHidden] public ValueInput B;
    [DoNotSerialize][PortLabelHidden] public ValueInput C;

    [DoNotSerialize]
    [PortLabelHidden]
    public ValueOutput result;

    protected override void Definition() {
      A = LhsPort = ValueInput<DValue>(LhsName, LhsDefaultValue);
      B = RhsPort = ValueInput<DValue>(RhsName, RhsDefaultValue);
      C = AuxPort = ValueInput<DValue>(AuxName, AuxDefaultValue);

      DValue ComputeFromFlow(Flow flow) {
        DValue a = flow.GetValue<DValue>(A);
        DValue b = flow.GetValue<DValue>(B);
        DValue c = flow.GetValue<DValue>(C);

        TData data = GetData(flow, a, b, c);

        int rows = Math.Max(a.Rows, Math.Max(b.Rows, c.Rows));
        int cols = Math.Max(a.Columns, Math.Max(b.Columns, c.Columns));
        double[] result = new double[rows * cols];
        for (int row = 0; row < rows; ++row) {
          for (int col = 0; col < cols; ++col) {
            result[row * cols + col] = ComputeElement(data, a[row, col], b[row, col], c[row, col]);
          }
        }
        return new DValue { ValueArray = result, Columns = cols, Rows = rows };
      }

      result = ValueOutput<DValue>(ResultName, DNodeUtils.CachePerFrame(ComputeFromFlow));
    }

    protected virtual string LhsName => "A";
    protected virtual string RhsName => "B";
    protected virtual string AuxName => "C";
    protected virtual ValueInput LhsPort { set { A = value; } }
    protected virtual ValueInput RhsPort { set { B = value; } }
    protected virtual ValueInput AuxPort { set { C = value; } }
    protected virtual double LhsDefaultValue => 0.0;
    protected virtual double RhsDefaultValue => 0.0;
    protected virtual double AuxDefaultValue => 0.0;
    protected virtual string ResultName => "result";
    protected abstract TData GetData(Flow flow, DValue a, DValue b, DValue c);
    protected abstract double ComputeElement(TData data, double a, double b, double c);
  }
}
