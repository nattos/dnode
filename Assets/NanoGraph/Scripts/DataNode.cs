using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

namespace NanoGraph {
  [Serializable]
  public abstract class DataNode : IDataNode, IEditableAttributeProvider {
    [EditableAttribute]
    public string Name;

    [DoNotSerialize]
    public NanoGraph Graph { get; set; }
    [DoNotSerialize]
    public abstract DataSpec InputSpec { get; }
    [DoNotSerialize]
    public abstract DataSpec OutputSpec { get; }
    [DoNotSerialize]
    public DataNodeCacheData CacheData { get; set; }

    [DoNotSerialize]
    private readonly List<string> _errorMessages = new List<string>();

    public DataNode() {
      Name = GetType().Name;
    }

    public void Validate(List<Action> cleanupActions) {
      _errorMessages.Clear();
    }

    public override string ToString() {
      return $"{Name} ({GetType().Name})";
    }

    public string DebugInfoDump {
      get {
        StringBuilder output = new StringBuilder();
        output.AppendLine(this.ToString());
        output.AppendLine();
        DataSpec inputSpec = InputSpec;
        DataSpec outputSpec = OutputSpec;
        output.AppendLine($"Input: {{");
        foreach (var field in InputSpec.Fields) {
          output.AppendLine($"  {field};");
        }
        output.AppendLine($"}}");
        output.AppendLine($"Output: {{");
        foreach (var field in OutputSpec.Fields) {
          output.AppendLine($"  {field};");
        }
        output.AppendLine($"}}");
        var inputEdges = Graph.GetInputEdges(this);
        var outputEdges = Graph.GetOutputEdges(this);
        output.AppendLine($"Ins ({inputEdges.Length}): [");
        foreach (var edge in inputEdges) {
          output.AppendLine($"  {edge},");
        }
        output.AppendLine($"]");
        output.AppendLine($"Outs ({outputEdges.Length}): [");
        foreach (var edge in outputEdges) {
          output.AppendLine($"  {edge},");
        }
        output.AppendLine($"]");
        return output.ToString();
      }
    }

    public string ShortName => string.IsNullOrWhiteSpace(Name) ? GetType().Name : Name;

    public virtual IReadOnlyList<EditableAttribute> EditableAttributes => GetCachedEditableAttributes(GetType());

    private static Dictionary<Type, IReadOnlyList<EditableAttribute>> _cachedEditableAttibutes = new Dictionary<Type, IReadOnlyList<EditableAttribute>>();

    private static IReadOnlyList<EditableAttribute> GetCachedEditableAttributes(Type type) {
      if (_cachedEditableAttibutes.TryGetValue(type, out var cached)) {
        return cached;
      }
      List<EditableAttribute> result = new List<EditableAttribute>();
      foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)) {
        bool isEditable = field.GetCustomAttributes(typeof(EditableAttributeAttribute), inherit: true).Cast<EditableAttributeAttribute>().Any();
        if (!isEditable) {
          continue;
        }
        result.Add(new EditableAttribute {
          Name = field.Name,
          Type = field.FieldType,
          Getter = node => field.GetValue(node),
          Setter = (node, value) => field.SetValue(node, value),
        });
      }
      var outResult = result.ToArray();
      _cachedEditableAttibutes[type] = outResult;
      return outResult;
    }
  }
}
