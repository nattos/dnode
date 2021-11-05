using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace DNode {
  public class AttributeCache {
    private Metadata _metadata;
    private readonly Dictionary<Type, Attribute> _cache = new Dictionary<Type, Attribute>();

    public bool TryGetAttribute<T>(Metadata metadata, out T attrib) where T : Attribute {
      if (_metadata != metadata) {
        _metadata = metadata;
        _cache.Clear();
      } else if (_cache.TryGetValue(typeof(T), out Attribute cachedAttrib)) {
        attrib = (T)cachedAttrib;
        return cachedAttrib != null;
      }
      UnityEditorUtils.TryGetAttribute<T>(metadata, out attrib);
      _cache[typeof(T)] = attrib;
      return attrib != null;
    }
  }
}
