using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Unity.VisualScripting;
using UnityObject = UnityEngine.Object;

namespace NanoGraph.VisualScripting {
  public class NanoGraphUnitOptionTree : ExtensibleFuzzyOptionTree {
    #region Initialization

    public NanoGraphUnitOptionTree(GUIContent label) : base(label) {
      favorites = new Favorites(this);

      showBackgroundWorkerProgress = true;
    }

    public override IFuzzyOption Option(object item) {
      if (item is Namespace @namespace) {
        return new NamespaceOption(@namespace, true);
      }
      if (item is Type type) {
        return new TypeOption(type, true);
      }

      return base.Option(item);
    }

    private static readonly HashSet<Type> _allowedUnitTypes = new HashSet<Type> {
      // typeof (SubgraphUnit),
      typeof (GraphOutput),
      typeof (GraphInput),
    };

    private class AliasInfo {
      public readonly string AliasKey;
      public readonly Action<IUnit> PreconfigureUnitHandler;
      public readonly Func<IUnitOption, string> LabelHandler;
      public readonly Func<IUnitOption, string> HaystackHandler;
      public readonly Func<IUnitOption, string, string> SearchResultLabelHandler;

      public AliasInfo(string aliasKey, Action<IUnit> preconfigureUnitHandler = null, Func<IUnitOption, string> labelHandler = null, Func<IUnitOption, string> haystackHandler = null, Func<IUnitOption, string, string> searchResultLabelHandler = null) {
        AliasKey = aliasKey;
        PreconfigureUnitHandler = preconfigureUnitHandler;
        LabelHandler = labelHandler;
        HaystackHandler = haystackHandler;
        SearchResultLabelHandler = searchResultLabelHandler;
      }
    }

    private static readonly Lazy<(IReadOnlyDictionary<Type, (AliasInfo mainAlias, IReadOnlyList<AliasInfo> alternatives)> typeMap, IReadOnlyDictionary<string, AliasInfo> keyMap)> _aliases = new Lazy<(IReadOnlyDictionary<Type, (AliasInfo, IReadOnlyList<AliasInfo>)>, IReadOnlyDictionary<string, AliasInfo>)>(() => {
      Dictionary<Type, (AliasInfo, IReadOnlyList<AliasInfo>)> typeMap = new Dictionary<Type, (AliasInfo, IReadOnlyList<AliasInfo>)>();
      Dictionary<string, AliasInfo> keyMap = new Dictionary<string, AliasInfo>();

      foreach (Type type in Codebase.runtimeTypes) {
        AliasInfo mainAlias = null;
        List<AliasInfo> typeAliases = new List<AliasInfo>();
        void MapSubOption(AliasInfo aliasInfo, bool isMainAlias = false) {
          keyMap[aliasInfo.AliasKey] = aliasInfo;
          if (!isMainAlias) {
            typeAliases.Add(aliasInfo);
          }
        }

        int aliasIndex = 0;
        string MakeAliasKey() {
          string aliasKey = $"{type.FullName}#{aliasIndex}";
          ++aliasIndex;
          return aliasKey;
        }

        IAliasProvider aliasProvider = null;
        AliasAttribute attrib = type.GetCustomAttributes(typeof(AliasAttribute), inherit: true).OfType<AliasAttribute>().FirstOrDefault();
        if (attrib != null) {
          aliasProvider = Activator.CreateInstance(attrib.AliasType) as IAliasProvider;
        }
        if (aliasProvider != null) {
          {
            string[] aliases = aliasProvider.GetAliases()?.ToArray() ?? Array.Empty<string>();
            if (aliases.Length > 0) {
              var subOption = new AliasInfo(
                  MakeAliasKey(),
                  haystackHandler: option => option.haystack + " " + string.Join(" ", aliases)
              );
              mainAlias = subOption;
              MapSubOption(subOption, true);
            }
          }

          IEnumerable<(string label, string[] aliases, Action<IUnit> configurer)> alternatives = aliasProvider.GetAlternatives() ?? Array.Empty<(string, string[], Action<IUnit>)>();
          foreach (var alternative in alternatives) {
            string label = alternative.label.Prettify();
            string haystack = label;
            string[] aliases = alternative.aliases ?? Array.Empty<string>();
            if (aliases.Length > 0) {
              haystack = label + " " + string.Join(" ", aliases);
            }

            var subOption = new AliasInfo(
                MakeAliasKey(),
                searchResultLabelHandler: (_, _) => label,
                labelHandler: _ => label,
                haystackHandler: _ => haystack,
                preconfigureUnitHandler: alternative.configurer
            );
            MapSubOption(subOption);
          }
        }
        typeMap[type] = (mainAlias, typeAliases);
      }
      return (typeMap, keyMap);
    });

    private static AliasInfo GetAliasInfo(string key) {
      return _aliases.Value.keyMap.GetValueOrDefault(key);
    }

    public override void Prewarm() {
      filter = filter ?? UnitOptionFilter.Any;

      try {
        options = new HashSet<IUnitOption>(
            UnitBase.Subset(filter, reference)
            .Where(option => typeof(BaseNode).IsAssignableFrom(option.unitType) || _allowedUnitTypes.Contains(option.unitType))
            .SelectMany(option => {
              IUnitOption newOption = option;
              IEnumerable<IUnitOption> subOptions = Enumerable.Empty<IUnitOption>();

              if (_aliases.Value.typeMap.TryGetValue(option.unitType, out var entry)) {
                if (entry.mainAlias != null) {
                  var subOption = new AliasUnitOption(option, entry.mainAlias.AliasKey);
                  newOption = subOption;
                }

                subOptions = subOptions.Concat(entry.alternatives.Select(alternative => {
                  var subOption = new AliasUnitOption(option, alternative.AliasKey);
                  return subOption;
                }));
              }
              subOptions = subOptions.Append(newOption);
              return subOptions;
            })
        );
      } catch (Exception ex) {
        Debug.LogError($"Failed to fetch node options for fuzzy finder (error log below).\nTry rebuilding the node options from '{UnitOptionUtility.GenerateUnitDatabasePath}'.\n\n{ex}");
        options = new HashSet<IUnitOption>();
      }

      typesWithMembers = new HashSet<Type>();

      foreach (var option in options) {
        if (option is IMemberUnitOption memberUnitOption && memberUnitOption.targetType != null) {
          typesWithMembers.Add(memberUnitOption.targetType);
        }
      }
    }

    private HashSet<IUnitOption> options;
    private HashSet<Type> typesWithMembers;

    #endregion


    #region Configuration

    public UnitOptionFilter filter { get; set; }
    public GraphReference reference { get; set; }
    public bool includeNone { get; set; }
    public bool surfaceCommonTypeLiterals { get; set; }
    public object[] rootOverride { get; set; }

    public FlowGraph graph => reference.graph as FlowGraph;
    public GameObject self => reference.self;

    public ActionDirection direction { get; set; } = ActionDirection.Any;

    #endregion


    #region Hierarchy

    private readonly FuzzyGroup enumsGroup = new FuzzyGroup("(Enums)", typeof(Enum).Icon());
    private readonly FuzzyGroup selfGroup = new FuzzyGroup("This", typeof(GameObject).Icon());

    private IEnumerable<UnitCategory> SpecialCategories() {
      // yield return new UnitCategory("Codebase");
      // yield return new UnitCategory("Events");
      // yield return new UnitCategory("Variables");
      // yield return new UnitCategory("Math");
      yield return new UnitCategory("Nesting");
      yield return new UnitCategory("Graphs");
    }

    public override IEnumerable<object> Root() {
      if (rootOverride != null && rootOverride.Length > 0) {
        foreach (var item in rootOverride) {
          yield return item;
        }

        yield break;
      }

      if (filter.CompatibleOutputType != null) {
        var outputType = filter.CompatibleOutputType;
        var outputTypeLiteral = options.FirstOrDefault(option => option is LiteralOption literalOption && literalOption.literalType == outputType);

        if (outputTypeLiteral != null) {
          yield return outputTypeLiteral;
        }

        HashSet<Type> noSurfaceConstructors = new HashSet<Type>() { typeof(string), typeof(object) };
        if (!noSurfaceConstructors.Contains(outputType)) {
          var outputTypeConstructors = options.Where(option => option is InvokeMemberOption invokeMemberOption &&
              invokeMemberOption.targetType == outputType &&
              invokeMemberOption.unit.member.isConstructor);

          foreach (var outputTypeConstructor in outputTypeConstructors) {
            yield return outputTypeConstructor;
          }
        }

        if (outputType == typeof(bool)) {
          foreach (var logicOperation in CategoryChildren(new UnitCategory("Logic"))) {
            yield return logicOperation;
          }
        }

        if (outputType.IsNumeric()) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Scalar"))) {
            yield return mathOperation;
          }
        }

        if (outputType == typeof(Vector2)) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Vector 2"))) {
            yield return mathOperation;
          }
        }

        if (outputType == typeof(Vector3)) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Vector 3"))) {
            yield return mathOperation;
          }
        }

        if (outputType == typeof(Vector4)) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Vector 4"))) {
            yield return mathOperation;
          }
        }
      }

      if (surfaceCommonTypeLiterals) {
        foreach (var commonType in EditorTypeUtility.commonTypes) {
          if (commonType == filter.CompatibleOutputType) {
            continue;
          }
          var commonTypeLiteral = options.FirstOrDefault(option => option is LiteralOption literalOption && literalOption.literalType == commonType);
          if (commonTypeLiteral != null) {
            yield return commonTypeLiteral;
          }
        }
      }

      if (filter.CompatibleInputType != null) {
        var inputType = filter.CompatibleInputType;

        if (!inputType.IsPrimitive && inputType != typeof(object)) {
          yield return inputType;
        }

        if (inputType == typeof(bool)) {
          yield return options.Single(o => o.UnitIs<If>());
          yield return options.Single(o => o.UnitIs<SelectUnit>());
        }

        if (inputType == typeof(bool) || inputType.IsNumeric()) {
          foreach (var logicOperation in CategoryChildren(new UnitCategory("Logic"))) {
            yield return logicOperation;
          }
        }

        if (inputType.IsNumeric()) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Scalar"))) {
            yield return mathOperation;
          }
        }

        if (inputType == typeof(Vector2)) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Vector 2"))) {
            yield return mathOperation;
          }
        }

        if (inputType == typeof(Vector3)) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Vector 3"))) {
            yield return mathOperation;
          }
        }

        if (inputType == typeof(Vector4)) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Math/Vector 4"))) {
            yield return mathOperation;
          }
        }

        if (typeof(IEnumerable).IsAssignableFrom(inputType) && (inputType != typeof(string) && inputType != typeof(Transform))) {
          foreach (var mathOperation in CategoryChildren(new UnitCategory("Collections"), false)) {
            yield return mathOperation;
          }
        }

        if (typeof(IList).IsAssignableFrom(inputType)) {
          foreach (var listOperation in CategoryChildren(new UnitCategory("Collections/Lists"))) {
            yield return listOperation;
          }
        }

        if (typeof(IDictionary).IsAssignableFrom(inputType)) {
          foreach (var dictionaryOperation in CategoryChildren(new UnitCategory("Collections/Dictionaries"))) {
            yield return dictionaryOperation;
          }
        }
      }

      if (UnityAPI.Await
          (
              () => {
                if (self != null) {
                  selfGroup.label = self.name;
                  selfGroup.icon = self.Icon();
                  return true;
                }

                return false;
              }
          )
      ) {
        yield return selfGroup;
      }

      foreach (var category in options.Select(option => option.category?.root)
               .NotNull()
               .Concat(SpecialCategories())
               .Distinct()
               .OrderBy(c => c.name)) {
        yield return category;
      }

      foreach (var extensionRootItem in base.Root()) {
        yield return extensionRootItem;
      }

      if (filter.Self) {
        var self = options.FirstOrDefault(option => option.UnitIs<This>());

        if (self != null) {
          yield return self;
        }
      }

      foreach (var unit in CategoryChildren(null)) {
        yield return unit;
      }

      if (includeNone) {
        yield return null;
      }
    }

    public override IEnumerable<object> Children(object parent) {
      if (parent is Namespace @namespace) {
        return NamespaceChildren(@namespace);
      } else if (parent is Type type) {
        return TypeChildren(type);
      } else if (parent == enumsGroup) {
        return EnumsChildren();
      } else if (parent == selfGroup) {
        return SelfChildren();
      } else if (parent is UnitCategory unitCategory) {
        return CategoryChildren(unitCategory);
      } else if (parent is VariableKind variableKind) {
        return VariableKindChildren(variableKind);
      } else {
        return base.Children(parent);
      }
    }

    private IEnumerable<object> SelfChildren() {
      yield return typeof(GameObject);

      // Self components can be null if no script is assigned to them
      // https://support.ludiq.io/forums/5-bolt/topics/817-/
      foreach (var selfComponentType in UnityAPI.Await(() => self.GetComponents<Component>().NotUnityNull().Select(c => c.GetType()))) {
        yield return selfComponentType;
      }
    }

    private IEnumerable<object> CodebaseChildren() {
      foreach (var rootNamespace in typesWithMembers.Where(t => !t.IsEnum)
               .Select(t => t.Namespace().Root)
               .OrderBy(ns => ns.DisplayName(false))
               .Distinct()) {
        yield return rootNamespace;
      }

      if (filter.Literals && options.Any(option => option is LiteralOption literalOption && literalOption.literalType.IsEnum)) {
        yield return enumsGroup;
      }
    }

    private IEnumerable<object> MathChildren() {
      foreach (var mathMember in GetMembers(typeof(Mathf)).Where(option => !((MemberUnit)option.unit).member.requiresTarget)) {
        yield return mathMember;
      }
    }

    private IEnumerable<object> TimeChildren() {
      foreach (var timeMember in GetMembers(typeof(Time)).Where(option => !((MemberUnit)option.unit).member.requiresTarget)) {
        yield return timeMember;
      }
    }

    private IEnumerable<object> NestingChildren() {
      foreach (var nester in options.Where(option => option.UnitIs<IGraphNesterElement>() && ((IGraphNesterElement)option.unit).nest.macro == null)
               .OrderBy(option => option.label)) {
        yield return nester;
      }
    }

    private IEnumerable<object> MacroChildren() {
      foreach (var macroNester in options.Where(option => option.UnitIs<IGraphNesterElement>() && ((IGraphNesterElement)option.unit).nest.macro != null)
               .OrderBy(option => option.label)) {
        yield return macroNester;
      }
    }

    private IEnumerable<object> VariablesChildren() {
      yield return VariableKind.Flow;
      yield return VariableKind.Graph;
      yield return VariableKind.Object;
      yield return VariableKind.Scene;
      yield return VariableKind.Application;
      yield return VariableKind.Saved;
    }

    private IEnumerable<object> VariableKindChildren(VariableKind kind) {
      foreach (var variable in options.OfType<IUnifiedVariableUnitOption>()
               .Where(option => option.kind == kind)
               .OrderBy(option => option.name)) {
        yield return variable;
      }
    }

    private IEnumerable<object> NamespaceChildren(Namespace @namespace) {
      foreach (var childNamespace in GetChildrenNamespaces(@namespace)) {
        yield return childNamespace;
      }

      foreach (var type in GetNamespaceTypes(@namespace)) {
        yield return type;
      }
    }

    private IEnumerable<Namespace> GetChildrenNamespaces(Namespace @namespace) {
      if (!@namespace.IsGlobal) {
        foreach (var childNamespace in typesWithMembers.Where(t => !t.IsEnum)
                 .SelectMany(t => t.Namespace().AndAncestors())
                 .Distinct()
                 .Where(ns => ns.Parent == @namespace)
                 .OrderBy(ns => ns.DisplayName(false))) {
          yield return childNamespace;
        }
      }
    }

    private IEnumerable<Type> GetNamespaceTypes(Namespace @namespace) {
      foreach (var type in typesWithMembers.Where(t => t.Namespace() == @namespace && !t.IsEnum)
               .OrderBy(t => t.DisplayName())) {
        yield return type;
      }
    }

    private IEnumerable<object> TypeChildren(Type type) {
      foreach (var literal in options.Where(option => option is LiteralOption literalOption && literalOption.literalType == type)) {
        yield return literal;
      }

      foreach (var expose in options.Where(option => option is ExposeOption exposeOption && exposeOption.exposedType == type)) {
        yield return expose;
      }

      if (type.IsStruct()) {
        foreach (var createStruct in options.Where(option => option is CreateStructOption createStructOption && createStructOption.structType == type)) {
          yield return createStruct;
        }
      }

      foreach (var member in GetMembers(type)) {
        yield return member;
      }
    }

    private IEnumerable<IUnitOption> GetMembers(Type type) {
      foreach (var member in options.Where(option => option is IMemberUnitOption memberUnitOption && memberUnitOption.targetType == type && option.unit.canDefine)
               .OrderBy(option => BoltCore.Configuration.groupInheritedMembers && ((MemberUnit)option.unit).member.isPseudoInherited)
               .ThenBy(option => option.order)
               .ThenBy(option => option.label)) {
        yield return member;
      }
    }

    private IEnumerable<object> EnumsChildren() {
      foreach (var literal in options.Where(option => option is LiteralOption literalOption && literalOption.literalType.IsEnum)
               .OrderBy(option => option.label)) {
        yield return literal;
      }
    }

    private IEnumerable<object> CategoryChildren(UnitCategory category, bool subCategories = true) {
      if (category != null && subCategories) {
        foreach (var subCategory in options.SelectMany(option => option.category == null ? Enumerable.Empty<UnitCategory>() : option.category.AndAncestors())
                 .Distinct()
                 .Where(c => c.parent == category)
                 .OrderBy(c => c.name)) {
          yield return subCategory;
        }
      }

      foreach (var unit in options.Where(option => option.category == category)
               .Where(option => !option.unitType.HasAttribute<SpecialUnitAttribute>())
               .OrderBy(option => option.order)
               .ThenBy(option => option.label)) {
        yield return unit;
      }

      if (category != null) {
        if (category.root.name == "Events") {
          foreach (var eventChild in EventsChildren(category)) {
            yield return eventChild;
          }
        } else if (category.fullName == "Codebase") {
          foreach (var codebaseChild in CodebaseChildren()) {
            yield return codebaseChild;
          }
        } else if (category.fullName == "Variables") {
          foreach (var variableChild in VariablesChildren()) {
            yield return variableChild;
          }
        } else if (category.fullName == "Math") {
          foreach (var mathChild in MathChildren()) {
            yield return mathChild;
          }
        } else if (category.fullName == "Time") {
          foreach (var timeChild in TimeChildren()) {
            yield return timeChild;
          }
        } else if (category.fullName == "Nesting") {
          foreach (var nestingChild in NestingChildren()) {
            yield return nestingChild;
          }
        } else if (category.fullName == "Graphs") {
          foreach (var macroChild in MacroChildren()) {
            yield return macroChild;
          }
        }
      }
    }

    private IEnumerable<object> EventsChildren(UnitCategory category) {
      foreach (var unit in options.Where(option => option.UnitIs<IEventUnit>() && option.category == category)
               .OrderBy(option => option.order)
               .ThenBy(option => option.label)) {
        yield return unit;
      }
    }

    #endregion


    #region Search

    public override bool searchable { get; } = true;

    public override IEnumerable<ISearchResult> SearchResults(string query, CancellationToken cancellation) {
      foreach (var typeResult in typesWithMembers.Cancellable(cancellation).OrderableSearchFilter(query, t => t.DisplayName())) {
        yield return typeResult;
      }

      foreach (var optionResult in options.Cancellable(cancellation)
               .OrderableSearchFilter(query, o => o.haystack, o => o.formerHaystack)
               .WithoutInheritedDuplicates(r => r.result, cancellation)) {
        yield return optionResult;
      }
    }

    public override string SearchResultLabel(object item, string query) {
      if (item is Type type) {
        return TypeOption.SearchResultLabel(type, query);
      } else if (item is IUnitOption unitOption) {
        return unitOption.SearchResultLabel(query);
      } else {
        return base.SearchResultLabel(item, query);
      }
    }

    #endregion


    #region Favorites

    public override ICollection<object> favorites { get; }

    public override bool CanFavorite(object item) {
      return (item as IUnitOption)?.favoritable ?? false;
    }

    public override string FavoritesLabel(object item) {
      return SearchResultLabel(item, null);
    }

    public override void OnFavoritesChange() {
      BoltFlow.Configuration.Save();
    }

    private class Favorites : ICollection<object> {
      public Favorites(NanoGraphUnitOptionTree tree) {
        this.tree = tree;
      }

      private NanoGraphUnitOptionTree tree { get; }

      private IEnumerable<IUnitOption> options => tree.options.Where(option => BoltFlow.Configuration.favoriteUnitOptions.Contains(option.favoriteKey));

      public bool IsReadOnly => false;

      public int Count => BoltFlow.Configuration.favoriteUnitOptions.Count;

      public IEnumerator<object> GetEnumerator() {
        foreach (var option in options) {
          yield return option;
        }
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
      }

      public bool Contains(object item) {
        var option = (IUnitOption)item;

        return BoltFlow.Configuration.favoriteUnitOptions.Contains(option.favoriteKey);
      }

      public void Add(object item) {
        var option = (IUnitOption)item;

        BoltFlow.Configuration.favoriteUnitOptions.Add(option.favoriteKey);
      }

      public bool Remove(object item) {
        var option = (IUnitOption)item;

        return BoltFlow.Configuration.favoriteUnitOptions.Remove(option.favoriteKey);
      }

      public void Clear() {
        BoltFlow.Configuration.favoriteUnitOptions.Clear();
      }

      public void CopyTo(object[] array, int arrayIndex) {
        if (array == null) {
          throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0) {
          throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (array.Length - arrayIndex < Count) {
          throw new ArgumentException();
        }

        var i = 0;

        foreach (var item in this) {
          array[i + arrayIndex] = item;
          i++;
        }
      }
    }

    #endregion


    private class AliasUnitOption : IUnitOption {
      public IUnitOption Target { get; private set; }
      public string AliasKey { get; private set; }
      public Action<IUnit> PreconfigureUnitHandler => _aliasInfo.Value?.PreconfigureUnitHandler;
      public Func<IUnitOption, string> LabelHandler => _aliasInfo.Value?.LabelHandler;
      public Func<IUnitOption, string> HaystackHandler => _aliasInfo.Value?.HaystackHandler;
      public Func<IUnitOption, string, string> SearchResultLabelHandler => _aliasInfo.Value?.SearchResultLabelHandler;

      private readonly Lazy<AliasInfo> _aliasInfo;

      public AliasUnitOption() {}
      public AliasUnitOption(IUnitOption target, string aliasKey) {
        Target = target;
        AliasKey = aliasKey;
        _aliasInfo = new Lazy<AliasInfo>(() => GetAliasInfo(AliasKey));
      }

      public UnitCategory category => Target.category;
      public HashSet<Type> valueOutputTypes => Target.valueOutputTypes;
      public HashSet<Type> valueInputTypes => Target.valueInputTypes;
      public int controlOutputCount => Target.controlOutputCount;
      public int controlInputCount => Target.controlInputCount;
      public Type unitType => Target.unitType;
      public bool favoritable => Target.favoritable;
      public string favoriteKey => Target.favoriteKey;
      public string formerHaystack => Target.formerHaystack;
      public int order => Target.order;
      public HashSet<string> sourceScriptGuids => Target.sourceScriptGuids;
      public IUnit unit => Target.unit;
      public string haystack => HaystackHandler?.Invoke(Target) ?? Target.haystack;

      public void Deserialize(UnitOptionRow row) {
        AliasKey = row.tag2;
        Debug.Log(AliasKey);
        var optionType = Codebase.DeserializeType(row.tag1);
        IUnitOption option;
        option = (IUnitOption)Activator.CreateInstance(optionType);
        option.Deserialize(row);
        Target = option;
      }
      public IUnit InstantiateUnit() => Target.InstantiateUnit();
      public void PreconfigureUnit(IUnit unit) { Target.PreconfigureUnit(unit); PreconfigureUnitHandler?.Invoke(unit); }
      public string SearchResultLabel(string query) => SearchResultLabelHandler?.Invoke(Target, query) ?? Target.SearchResultLabel(query);
      public UnitOptionRow Serialize() {
        Debug.Log($"Serialize: {AliasKey}");
        UnitOptionRow row = Target.Serialize();
        row.tag1 = row.optionType;
        row.tag2 = AliasKey;
        row.optionType = Codebase.SerializeType(GetType());
        return row;
      }

      public object value => this;
      public bool parentOnly => Target.parentOnly;
      public string label => LabelHandler?.Invoke(Target) ?? Target.label;
      public EditorTexture icon => Target.icon;
      public GUIStyle style => Target.style;
      public string headerLabel => Target.headerLabel;
      public bool showHeaderIcon => Target.showHeaderIcon;
      public bool hasFooter => Target.hasFooter;
      public float GetFooterHeight(float width) => Target.GetFooterHeight(width);
      public void OnFooterGUI(Rect position) => Target.OnFooterGUI(position);
      public void OnPopulate() => Target.OnPopulate();
    }
  }
}
