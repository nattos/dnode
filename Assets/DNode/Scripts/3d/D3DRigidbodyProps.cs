using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  public class D3DRigidbodyProps : DFrameObjectWithDataUnit<D3DRigidbodyProps.Data> {
    public struct Data {
      public DValue? Mass;
      public DValue? Drag;
      public DValue? AngularDrag;
      public DValue? UseGravity;
      public DValue? IsKinematic;
    }

    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 64, 1)][LogScale][ShortEditor] public ValueInput Mass;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 0)][LogScale][ShortEditor] public ValueInput Drag;
    [DoNotSerialize][PortLabelHidden][Scalar][Range(0, 16, 0.05)][LogScale][ShortEditor] public ValueInput AngularDrag;
    [DoNotSerialize][PortLabelHidden][Boolean][ShortEditor] public ValueInput UseGravity;
    [DoNotSerialize][PortLabelHidden][Boolean][ShortEditor] public ValueInput IsKinematic;

    [Inspectable] public D3DSpaceType Space = D3DSpaceType.Local;
    [Inspectable] public bool Relative = true;

    protected override void Definition() {
      base.Definition();
      Mass = ValueInput<DEvent>(nameof(Mass), DEvent.CreateImmediate(1.0, triggered: false));
      Drag = ValueInput<DEvent>(nameof(Drag), DEvent.CreateImmediate(0.0, triggered: false));
      AngularDrag = ValueInput<DEvent>(nameof(AngularDrag), DEvent.CreateImmediate(0.05, triggered: false));
      UseGravity = ValueInput<DEvent>(nameof(UseGravity), DEvent.CreateImmediate(1.0, triggered: false));
      IsKinematic = ValueInput<DEvent>(nameof(IsKinematic), DEvent.CreateImmediate(0.0, triggered: false));
    }

    protected override Data GetData(Flow flow, DFrameObject[] inputs) {
      return new Data {
        Mass = GetNullableDValueFromDEventInput(flow, Mass),
        Drag = GetNullableDValueFromDEventInput(flow, Drag),
        AngularDrag = GetNullableDValueFromDEventInput(flow, AngularDrag),
        UseGravity = GetNullableDValueFromDEventInput(flow, UseGravity),
        IsKinematic = GetNullableDValueFromDEventInput(flow, IsKinematic),
      };
    }

    protected override void ApplyToObject(Data data, int row, GameObject input) {
      var body = RigidbodyComponent.GetOrAdd(input);
      if (!body) {
        return;
      }
      body.Mass.MaybeSetValue(data.Mass?.FloatFromRow(row));
      body.Drag.MaybeSetValue(data.Drag?.FloatFromRow(row));
      body.AngularDrag.MaybeSetValue(data.AngularDrag?.FloatFromRow(row));
      body.UseGravity.MaybeSetValue(data.UseGravity?.BoolFromRow(row));
      body.IsKinematic.MaybeSetValue(data.IsKinematic?.BoolFromRow(row));
    }
  }
}
