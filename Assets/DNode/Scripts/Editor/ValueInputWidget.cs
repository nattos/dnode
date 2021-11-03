using System;
using Unity.VisualScripting;
using UnityEngine;

namespace DNode {
  [Widget(typeof(ValueInput))]
  public class DNodeValueInputWidget : ValueInputWidget {
    private bool _isDValueEditor = false;

    public DNodeValueInputWidget(FlowCanvas canvas, ValueInput port) : base(canvas, port) {}

    protected override bool showInspector => port.hasDefaultValue && (!port.hasValidConnection || _isDValueEditor);

    public override void DrawForeground() {
      metadata.isEditable = !port.hasValidConnection;
      base.DrawForeground();
    }

    protected override void CacheMetadata() {
      base.CacheMetadata();
      bool isDValue = inspectorMetadata != null && typeof(DValue).IsAssignableFrom(inspectorMetadata.definedType);
      bool isDEvent = inspectorMetadata != null && typeof(DEvent).IsAssignableFrom(inspectorMetadata.definedType);
      _isDValueEditor = isDValue || isDEvent;
      Type displayIconAsType = null;
      if (UnityEditorUtils.TryGetAttribute<ColorAttribute>(metadata, out _)) {
        displayIconAsType = typeof(Color);
      } else if (UnityEditorUtils.TryGetAttribute<VectorAttribute>(metadata, out var vectorAttrib)) {
        switch (vectorAttrib.Dims) {
          case 1:
            displayIconAsType = typeof(double);
            break;
          case 2:
            displayIconAsType = typeof(Vector2);
            break;
          case 3:
            displayIconAsType = typeof(Vector3);
            break;
          case 4:
            displayIconAsType = typeof(Vector4);
            break;
        }
      }
      //if (displayIconAsType == null) {
      //  if (isDValue) {
      //    ;
      //  } else if (isDEvent) {
      //    ;
      //  }
      //}
      // Note: No longer works as UnitPortDescription.icon is a property.
      // if (displayIconAsType != null) {
      //   description.icon = Icons.Type(displayIconAsType);
      // }
    }
  }
}
