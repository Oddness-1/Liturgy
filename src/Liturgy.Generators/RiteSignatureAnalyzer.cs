using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Liturgy.Generators;

/// <summary>
/// Checks rites at compile time against the rules Liturgy applies when it starts: the method must be static (or an
/// interface member), and its parameters must fit the rite. A rite that breaks them never runs, and without this the
/// only sign is a line in Liturgy's runtime report.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RiteSignatureAnalyzer : DiagnosticAnalyzer
{
    private const string CATEGORY = "Liturgy";

    internal static readonly DiagnosticDescriptor NOT_STATIC = new(
        "LIT001", "Rite on an instance method", "'{0}' is an instance method, so [Rite.{1}] never runs; make it static, or declare it on an interface",
        CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor BAD_PARAMETERS = new(
        "LIT002", "Parameters don't fit the rite", "[Rite.{0}] can't call '{1}' with these parameters; it takes {2}",
        CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NOT_ON_INTERFACES = new(
        "LIT003", "Rite not supported on interface members", "[Rite.{0}] isn't supported on an interface member; declare '{1}' static instead",
        CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor TARGET_ON_INTERFACE = new(
        "LIT004", "Target on an interface rite", "Target is ignored on interface rites, which fire on each implementing node; use Exclude to skip nodes",
        CATEGORY, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NO_SIGNAL_NAME = new(
        "LIT005", "OnSignal without a signal name", "[Rite.OnSignal] on '{0}' needs a signal name",
        CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor IN_GENERIC_CLASS = new(
        "LIT006", "Rite in a generic class", "'{0}' is in a generic class, so [Rite.{1}] never runs; Liturgy has no type arguments to use. Move it to a non-generic class.",
        CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor CANT_ASSIGN = new(
        "LIT008", "AssignNode on a member Liturgy can't assign", "[Rite.AssignNode] never assigns '{0}': {1}",
        CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [NOT_STATIC, BAD_PARAMETERS, NOT_ON_INTERFACES, TARGET_ON_INTERFACE, NO_SIGNAL_NAME, IN_GENERIC_CLASS, CANT_ASSIGN];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName("Liturgy.Rite") is not { } rite) return;
            if (start.Compilation.GetTypeByMetadataName("Godot.Node") is null) return;

            var rules = new RiteRules(start.Compilation);
            start.RegisterSymbolAction(symbol => AnalyzeMethod(symbol, rite, rules), SymbolKind.Method);
            start.RegisterSymbolAction(symbol => AnalyzeAssignNode(symbol, rite), SymbolKind.Field, SymbolKind.Property);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol rite, RiteRules rules)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Ordinary) return;

        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass || !SymbolEqualityComparer.Default.Equals(attributeClass.ContainingType, rite)) continue;

            var name = attributeClass.Name;
            if (name is "SuppressWarnings" or "Settings" or "AssignNode") continue;

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? method.Locations.FirstOrDefault();
            var onInterface = method.ContainingType.TypeKind == TypeKind.Interface;

            if (IsInGenericClass(method.ContainingType))
            {
                context.ReportDiagnostic(Diagnostic.Create(IN_GENERIC_CLASS, location, method.Name, name));
                continue;
            }

            if (!method.IsStatic && !onInterface)
            {
                context.ReportDiagnostic(Diagnostic.Create(NOT_STATIC, location, method.Name, name));
                continue;
            }

            if (method.Parameters.Any(parameter => parameter.Type.TypeKind == TypeKind.Error)) continue;

            if (!method.IsStatic)
            {
                AnalyzeInterfaceRite(context, attribute, name, method, location, rules);
                continue;
            }

            if (name == "OnSignal")
            {
                AnalyzeOnSignal(context, attribute, method, location, rules);
                continue;
            }

            if (rules.Check(name, attribute, method) is { } expected)
                context.ReportDiagnostic(Diagnostic.Create(BAD_PARAMETERS, location, name, method.Name, expected));
        }
    }

    private static void AnalyzeAssignNode(SymbolAnalysisContext context, INamedTypeSymbol rite)
    {
        var member = context.Symbol;

        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "AssignNode" } attributeClass || !SymbolEqualityComparer.Default.Equals(attributeClass.ContainingType, rite)) continue;

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? member.Locations.FirstOrDefault();

            if (member.IsStatic && IsInGenericClass(member.ContainingType))
            {
                context.ReportDiagnostic(Diagnostic.Create(IN_GENERIC_CLASS, location, member.Name, "AssignNode"));
                continue;
            }

            if (WhyUnassignable(member) is { } reason) context.ReportDiagnostic(Diagnostic.Create(CANT_ASSIGN, location, member.Name, reason));
        }
    }

    // The rules Liturgy applies when it scans for AssignNode members, which would otherwise skip the member at runtime.
    private static string? WhyUnassignable(ISymbol member)
    {
        var onInterface = member.ContainingType.TypeKind == TypeKind.Interface;

        if (!member.IsStatic && !onInterface)
            return "it's an instance member of a class; make it static, or declare it as a property on an interface that the node implements";

        if (member is IFieldSymbol { IsConst: true } or IFieldSymbol { IsReadOnly: true }) return "it's readonly";
        if (member is IPropertySymbol { SetMethod: null }) return "it has no setter";

        var type = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null
        };

        if (type is null || type.TypeKind == TypeKind.Error) return null;

        if (type.TypeKind == TypeKind.TypeParameter || (type is INamedTypeSymbol named && named.TypeArguments.Any(argument => argument.TypeKind == TypeKind.TypeParameter)))
            return "its type uses a type parameter, so it has no single node type to look for";

        if (type.TypeKind != TypeKind.Interface && !DerivesFromNode(type))
            return $"its type, {type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}, is neither a node type nor an interface";

        return null;
    }

    private static bool DerivesFromNode(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current is { Name: "Node", ContainingNamespace: { Name: "Godot", ContainingNamespace.IsGlobalNamespace: true } }) return true;
        }

        return false;
    }

    /// <summary>
    /// A class with type parameters, or nested in one. Generic interfaces are fine: each implementation supplies the arguments.
    /// </summary>
    private static bool IsInGenericClass(INamedTypeSymbol? type)
    {
        if (type is null || type.TypeKind == TypeKind.Interface) return false;

        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeParameters.Length > 0) return true;
        }

        return false;
    }

    private static void AnalyzeInterfaceRite(SymbolAnalysisContext context, AttributeData attribute, string name, IMethodSymbol method, Location? location, RiteRules rules)
    {
        if (name is "OnNodeAdded" or "OnNodeRemoved" or "OnTreeChanged")
        {
            context.ReportDiagnostic(Diagnostic.Create(NOT_ON_INTERFACES, location, name, method.Name));
            return;
        }

        if (RiteRules.Named(attribute, "Target") is string) context.ReportDiagnostic(Diagnostic.Create(TARGET_ON_INTERFACE, location));

        if (name == "OnSignal" && SignalName(attribute) is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(NO_SIGNAL_NAME, location, method.Name));
            return;
        }

        if (rules.CheckInterfaceLifecycle(name, method) is { } expected)
            context.ReportDiagnostic(Diagnostic.Create(BAD_PARAMETERS, location, name, method.Name, expected));
    }

    private static void AnalyzeOnSignal(SymbolAnalysisContext context, AttributeData attribute, IMethodSymbol method, Location? location, RiteRules rules)
    {
        if (SignalName(attribute) is not { } signal)
        {
            context.ReportDiagnostic(Diagnostic.Create(NO_SIGNAL_NAME, location, method.Name));
            return;
        }

        // Only checkable when the first parameter is a node type that declares the signal; otherwise any node may emit it.
        if (method.Parameters.Length == 0 || method.Parameters[0].Type is not INamedTypeSymbol { TypeKind: TypeKind.Class } source || !rules.IsNode(source)) return;
        if (rules.FindSignal(source, signal) is not { } arguments) return;

        if (rules.CheckBinding(method, [source], arguments) is { } expected)
            context.ReportDiagnostic(Diagnostic.Create(BAD_PARAMETERS, location, "OnSignal", method.Name, expected));
    }

    private static string? SignalName(AttributeData attribute) =>
        attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string signal && !string.IsNullOrWhiteSpace(signal) ? signal : null;
}

/// <summary>
/// The parameter rules for each rite, mirroring Liturgy's runtime checks. Most rites take a value and/or a source node
/// in any order (a "slot" each); some accept one of several node families.
/// </summary>
internal sealed class RiteRules
{
    private readonly Compilation _compilation;
    private readonly INamedTypeSymbol _godotObject;
    private readonly INamedTypeSymbol _node;
    private readonly INamedTypeSymbol? _variant;
    private readonly Dictionary<string, (Slot[][] Alternatives, string Description)> _rites;

    public RiteRules(Compilation compilation)
    {
        _compilation = compilation;
        _godotObject = Type("Godot.GodotObject")!;
        _node = Type("Godot.Node")!;
        _variant = Type("Godot.Variant");

        var index = Exact("an int or long index", "System.Int32", "System.Int64");

        _rites = new Dictionary<string, (Slot[][], string)>
        {
            ["OnTimeout"] = Rite("nothing, or the Timer", [Derived("Timer")]),
            ["OnButtonPressed"] = Rite("nothing, or the BaseButton", [Derived("BaseButton")]),
            ["OnButtonUp"] = Rite("nothing, or the BaseButton", [Derived("BaseButton")]),
            ["OnButtonDown"] = Rite("nothing, or the BaseButton", [Derived("BaseButton")]),
            ["OnButtonToggled"] = Rite("a bool and/or the BaseButton, in any order", [Exact("bool", "System.Boolean"), Derived("BaseButton")]),
            ["OnColorChanged"] = Rite("a Color and/or the ColorPicker or ColorPickerButton, in any order",
                [Exact("Color", "Godot.Color"), Derived("ColorPicker")], [Exact("Color", "Godot.Color"), Derived("ColorPickerButton")]),
            ["OnFileSelected"] = Rite("a string path and/or the FileDialog, in any order", [Exact("string", "System.String"), Derived("FileDialog")]),
            ["OnDirSelected"] = Rite("a string path and/or the FileDialog, in any order", [Exact("string", "System.String"), Derived("FileDialog")]),
            ["OnFocusEntered"] = Rite("nothing, or the Control", [Derived("Control")]),
            ["OnFocusExited"] = Rite("nothing, or the Control", [Derived("Control")]),
            ["OnResized"] = Rite("nothing, or the Control", [Derived("Control")]),
            ["OnMinimumSizeChanged"] = Rite("nothing, or the Control", [Derived("Control")]),
            ["OnMouseEntered"] = Rite("nothing, or a Control, CollisionObject2D or CollisionObject3D", [Derived("Control")], [Derived("CollisionObject2D")], [Derived("CollisionObject3D")]),
            ["OnMouseExited"] = Rite("nothing, or a Control, CollisionObject2D or CollisionObject3D", [Derived("Control")], [Derived("CollisionObject2D")], [Derived("CollisionObject3D")]),
            ["OnGuiInput"] = Rite("an InputEvent type and/or the Control, in any order", [Derived("InputEvent"), Derived("Control")]),
            ["OnItemSelected"] = Rite("an int or long index and/or the ItemList or OptionButton, in any order", [index, Derived("ItemList")], [index, Derived("OptionButton")]),
            ["OnItemActivated"] = Rite("an int or long index and/or the ItemList, in any order", [index, Derived("ItemList")]),
            ["OnTabChanged"] = Rite("an int or long index and/or the TabContainer or TabBar, in any order", [index, Derived("TabContainer")], [index, Derived("TabBar")]),
            ["OnDragStarted"] = Rite("nothing, or the Slider", [Derived("Slider")]),
            ["OnDragEnded"] = Rite("a bool and/or the Slider, in any order", [Exact("bool", "System.Boolean"), Derived("Slider")]),
            ["OnScrollStarted"] = Rite("nothing, or the ScrollContainer", [Exact("ScrollContainer", "Godot.ScrollContainer")]),
            ["OnScrollEnded"] = Rite("nothing, or the ScrollContainer", [Exact("ScrollContainer", "Godot.ScrollContainer")]),
            ["OnSortChildren"] = Rite("nothing, or the Container", [Derived("Container")]),
            ["OnTextChanged"] = Rite("a string and/or the LineEdit in any order, or just the TextEdit", [Exact("string", "System.String"), Derived("LineEdit")], [Derived("TextEdit")]),
            ["OnTextSubmitted"] = Rite("a string and/or the LineEdit, in any order", [Exact("string", "System.String"), Derived("LineEdit")]),
            ["OnValueChanged"] = Rite("a double and/or the Range, in any order", [Exact("double", "System.Double"), Derived("Range")]),
            ["OnVisibilityChanged"] = Rite("nothing, or a CanvasItem or Node3D", [Derived("CanvasItem")], [Derived("Node3D")]),
            ["OnAnimationFinished"] = Rite("a StringName or string and/or the AnimationPlayer, in any order", [Exact("StringName or string", "Godot.StringName", "System.String"), Derived("AnimationPlayer")]),
            ["OnAnimationStarted"] = Rite("a StringName or string and/or the AnimationPlayer, in any order", [Exact("StringName or string", "Godot.StringName", "System.String"), Derived("AnimationPlayer")]),
            ["OnAudioFinished"] = Rite("nothing, or an AudioStreamPlayer, AudioStreamPlayer2D or AudioStreamPlayer3D", [Derived("AudioStreamPlayer")], [Derived("AudioStreamPlayer2D")], [Derived("AudioStreamPlayer3D")]),
            ["OnBodyEntered"] = Rite("the body and/or the area, 2D or 3D but not mixed", [Derived("Node2D", but: "Area2D"), Derived("Area2D")], [Derived("Node3D", but: "Area3D"), Derived("Area3D")]),
            ["OnBodyExited"] = Rite("the body and/or the area, 2D or 3D but not mixed", [Derived("Node2D", but: "Area2D"), Derived("Area2D")], [Derived("Node3D", but: "Area3D"), Derived("Area3D")]),
            ["OnAreaEntered"] = Rite("the entering area and/or the source area, both Area2D or both Area3D", [Derived("Area2D"), Derived("Area2D")], [Derived("Area3D"), Derived("Area3D")]),
            ["OnAreaExited"] = Rite("the exiting area and/or the source area, both Area2D or both Area3D", [Derived("Area2D"), Derived("Area2D")], [Derived("Area3D"), Derived("Area3D")]),
            ["OnPopupHide"] = Rite("nothing, or the Popup", [Derived("Popup")]),
            ["OnCloseRequested"] = Rite("nothing, or the Window", [Derived("Window")]),
            ["OnConfirmed"] = Rite("nothing, or the AcceptDialog", [Derived("AcceptDialog")]),
            ["OnCanceled"] = Rite("nothing, or the AcceptDialog", [Derived("AcceptDialog")]),
            ["OnInput"] = Rite("nothing, or an InputEvent type", [Derived("InputEvent")]),
            ["OnUnhandledInput"] = Rite("nothing, or an InputEvent type", [Derived("InputEvent")]),
            ["OnUnhandledKeyInput"] = Rite("nothing, or an InputEvent type", [Derived("InputEvent")]),
            ["OnNodeAdded"] = Rite("nothing, or a node type or interface", [NodeOrInterface()]),
            ["OnNodeRemoved"] = Rite("nothing, or a node type or interface", [NodeOrInterface()]),
            ["OnTreeChanged"] = Rite("no parameters", new Slot[0])
        };
    }

    // Rites built on OnSignal: their source types and the signal's arguments.
    private static readonly Dictionary<string, (string[] Sources, string[] Arguments)> SIGNAL_RITES = new()
    {
        ["OnIdPressed"] = (["Godot.PopupMenu", "Godot.MenuButton"], ["System.Int64"]),
        ["OnMetaClicked"] = (["Godot.RichTextLabel"], ["Godot.Variant"]),
        ["OnScreenEntered"] = (["Godot.VisibleOnScreenNotifier2D", "Godot.VisibleOnScreenNotifier3D"], []),
        ["OnScreenExited"] = (["Godot.VisibleOnScreenNotifier2D", "Godot.VisibleOnScreenNotifier3D"], [])
    };

    /// <summary>
    /// Null if the method's parameters fit the rite, else a description of what it takes.
    /// </summary>
    public string? Check(string rite, AttributeData attribute, IMethodSymbol method)
    {
        var parameters = method.Parameters.Select(parameter => parameter.Type).ToArray();

        // Every rite can be called with nothing.
        if (parameters.Length == 0) return null;

        switch (rite)
        {
            case "OnReady":
            {
                // RootReady without a Target and Immediate pass the root; PerNode and targeted RootReady pass each node.
                var mode = Named(attribute, "Mode") is int value ? value : 0;
                var perNode = mode == 2 || (mode == 0 && Named(attribute, "Target") is string);
                var slot = perNode ? NodeOrInterface() : Exact("the root (Window, Viewport or Node)", "Godot.Window", "Godot.Viewport", "Godot.Node");
                return Fits(parameters, [slot]) ? null : perNode ? "nothing, or a node type or interface" : "nothing, or the root as Window, Viewport or Node (use Mode = PerNode or a Target for other nodes)";
            }

            case "OnProcess":
            case "OnPhysicsProcess":
            {
                var global = (Named(attribute, "Mode") is int value ? value : 0) == 0 && Named(attribute, "Target") is not string;
                var node = global ? Exact("the root (Window, Viewport or Node)", "Godot.Window", "Godot.Viewport", "Godot.Node") : NodeOrInterface();
                return Fits(parameters, [Exact("double", "System.Double"), node])
                    ? null
                    : global ? "a double delta and/or the root as Window, Viewport or Node (use Mode = PerNode or a Target for other nodes)" : "a double delta and/or a node type or interface, in any order";
            }
        }

        if (SIGNAL_RITES.TryGetValue(rite, out var signalRite))
        {
            var sources = signalRite.Sources.Select(Type).OfType<INamedTypeSymbol>().ToArray<ITypeSymbol>();
            var arguments = signalRite.Arguments.Select(Type).OfType<ITypeSymbol>().ToArray();
            return sources.Length == signalRite.Sources.Length && arguments.Length == signalRite.Arguments.Length ? CheckBinding(method, sources, arguments) : null;
        }

        if (!_rites.TryGetValue(rite, out var rule)) return null;
        return rule.Alternatives.Any(slots => Fits(parameters, slots)) ? null : rule.Description;
    }

    /// <summary>
    /// Lifecycle rites on interface members take nothing, or one delta or input event. Signal rites take the signal's
    /// arguments, which depend on the implementing node, so they're checked when Liturgy starts.
    /// </summary>
    public string? CheckInterfaceLifecycle(string rite, IMethodSymbol method)
    {
        var parameters = method.Parameters.Select(parameter => parameter.Type).ToArray();

        return rite switch
        {
            "OnReady" => parameters.Length == 0 ? null : "no parameters",
            "OnProcess" or "OnPhysicsProcess" => Fits(parameters, [Exact("double", "System.Double")]) ? null : "nothing, or a double delta",
            "OnInput" or "OnUnhandledInput" or "OnUnhandledKeyInput" => Fits(parameters, [Derived("InputEvent")]) ? null : "nothing, or an InputEvent type",
            _ => null
        };
    }

    /// <summary>
    /// The OnSignal rules: nothing, the source, all the signal's arguments, or the source followed by them.
    /// </summary>
    public string? CheckBinding(IMethodSymbol method, ITypeSymbol[] sources, ITypeSymbol[] arguments)
    {
        var parameters = method.Parameters.Select(parameter => parameter.Type).ToArray();
        var count = parameters.Length;
        var arity = arguments.Length;
        var firstIsSource = count > 0 && IsSourceType(parameters[0]);

        var sourceNames = string.Join(" or ", sources.Select(source => source.Name));
        var expected = arity == 0
            ? $"nothing, or the source ({sourceNames})"
            : $"nothing, the source ({sourceNames}), the signal's arguments ({string.Join(", ", arguments.Select(argument => argument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)))}), or the source followed by them";

        if (count == 0) return null;
        if (firstIsSource && count == arity + 1) return Overlaps(parameters[0], sources) && ArgumentsFit(parameters.Skip(1), arguments) ? null : expected;

        if (count == arity)
        {
            var takesSource = firstIsSource && arity == 1 && !IsGodotObject(arguments[0]);
            if (takesSource) return Overlaps(parameters[0], sources) ? null : expected;
            return ArgumentsFit(parameters, arguments) ? null : expected;
        }

        if (firstIsSource && count == 1) return Overlaps(parameters[0], sources) ? null : expected;

        return expected;
    }

    /// <summary>
    /// The argument types of a signal on a type: an engine signal's C# event, or a script's [Signal] delegate. Null if not found.
    /// </summary>
    public ITypeSymbol[]? FindSignal(INamedTypeSymbol type, string signal)
    {
        var eventName = ToPascalCase(signal);

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                switch (member)
                {
                    case INamedTypeSymbol { TypeKind: TypeKind.Delegate } handler when handler.Name == $"{signal}EventHandler" && handler.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Godot.SignalAttribute"):
                        return handler.DelegateInvokeMethod?.Parameters.Select(parameter => parameter.Type).ToArray();

                    case IEventSymbol @event when @event.Name == eventName && current.ContainingAssembly.Name == "GodotSharp" && @event.Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }:
                        return invoke.Parameters.Select(parameter => parameter.Type).ToArray();
                }
            }
        }

        return null;
    }

    public bool IsNode(ITypeSymbol type) => DerivesFrom(type, _node);
    public bool IsGodotObject(ITypeSymbol type) => DerivesFrom(type, _godotObject);

    public static object? Named(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name) return argument.Value.Value;
        }

        return null;
    }

    private bool IsSourceType(ITypeSymbol type) => IsNode(type) || type.TypeKind == TypeKind.Interface;

    private bool Overlaps(ITypeSymbol parameter, ITypeSymbol[] sources) =>
        parameter.TypeKind == TypeKind.Interface || sources.Any(source => DerivesFrom(parameter, source) || DerivesFrom(source, parameter));

    private bool ArgumentsFit(IEnumerable<ITypeSymbol> parameters, ITypeSymbol[] arguments)
    {
        var index = 0;
        foreach (var parameter in parameters)
        {
            var argument = arguments[index++];
            if (_variant is not null && (SymbolEqualityComparer.Default.Equals(parameter, _variant) || SymbolEqualityComparer.Default.Equals(argument, _variant))) continue;

            var parameterIsObject = IsGodotObject(parameter) || parameter.TypeKind == TypeKind.Interface;
            if (IsGodotObject(argument) != parameterIsObject) return false;
        }

        return true;
    }

    private static string ToPascalCase(string signal) =>
        string.Concat(signal.Split('_').Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));

    private bool Fits(ITypeSymbol[] parameters, Slot[] slots)
    {
        if (parameters.Length > slots.Length) return false;

        var used = new bool[slots.Length];
        foreach (var parameter in parameters)
        {
            var matched = false;
            for (var i = 0; i < slots.Length; i++)
            {
                if (used[i] || !slots[i].Accepts(parameter)) continue;
                used[i] = matched = true;
                break;
            }

            if (!matched) return false;
        }

        return true;
    }

    private static (Slot[][], string) Rite(string description, params Slot[][] alternatives) => (alternatives, description);

    private INamedTypeSymbol? Type(string metadataName) => _compilation.GetTypeByMetadataName(metadataName);

    private Slot Exact(string name, params string[] metadataNames)
    {
        var types = metadataNames.Select(Type).OfType<INamedTypeSymbol>().ToArray();
        return new Slot(name, type => types.Any(candidate => SymbolEqualityComparer.Default.Equals(type, candidate)));
    }

    private Slot Derived(string godotType, string? but = null)
    {
        var baseType = Type($"Godot.{godotType}");
        var excluded = but is null ? null : Type($"Godot.{but}");
        return new Slot(godotType, type => baseType is not null && DerivesFrom(type, baseType) && (excluded is null || !DerivesFrom(type, excluded)));
    }

    private Slot NodeOrInterface() => new("a node type or interface", type => IsNode(type) || type.TypeKind == TypeKind.Interface);

    private static bool DerivesFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
        }

        return false;
    }

    private sealed class Slot(string name, System.Func<ITypeSymbol, bool> accepts)
    {
        public string Name { get; } = name;
        public bool Accepts(ITypeSymbol type) => accepts(type);
    }
}
