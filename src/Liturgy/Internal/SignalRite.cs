using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// One method bound to one signal: <c>[Rite.OnSignal]</c>, a rite built on it (<c>[Rite.OnIdPressed]</c>), or a rite
/// declared on an interface member, which runs on each implementing node. Works out, per signal shape, how the
/// method's parameters line up with the signal's arguments, and calls it.
/// </summary>
internal sealed class SignalRite
{
    public required string HookName { get; init; }
    public required MethodInfo Method { get; init; }
    public required StringName Signal { get; init; }

    /// <summary>
    /// Node types this rite applies to. A node must be one of them.
    /// </summary>
    public Type[] SourceTypes { get; init; } = [typeof(Node)];

    /// <summary>
    /// The object that emits the signal for a given node, when that isn't the node itself (a MenuButton's PopupMenu).
    /// </summary>
    public Func<Node, GodotObject?>? Emitter { get; init; }

    public HashSet<string>? Targets { get; init; }
    public HashSet<string>? Excludes { get; init; }
    public int Priority { get; init; }

    /// <summary>
    /// Declared on an interface member: invoked on the node itself, so there is no source parameter.
    /// </summary>
    public bool OnInstance { get; init; }

    public string MethodName => $"{Method.DeclaringType?.FullName}.{Method.Name}";

    private readonly Dictionary<SignalShape, Binding?> _bindings = [];
    private bool _reportedFailure;

    public bool AppliesTo(Node node)
    {
        foreach (var type in SourceTypes)
        {
            if (type.IsInstanceOfType(node)) return true;
        }

        return false;
    }

    public GodotObject? EmitterFor(Node node) => Emitter is null ? node : Emitter(node);

    /// <summary>
    /// How this rite's parameters line up with a signal of the given shape, or null if they can't (reported once).
    /// </summary>
    public Binding? Bind(SignalShape shape)
    {
        if (_bindings.TryGetValue(shape, out var cached)) return cached;

        var binding = CreateBinding(shape);
        _bindings[shape] = binding;
        return binding;
    }

    private Binding? CreateBinding(SignalShape shape)
    {
        var parameters = Method.GetParameters();
        var count = parameters.Length;
        var arity = shape.Arity;

        var firstIsSource = !OnInstance && count > 0 && IsSourceType(parameters[0].ParameterType);

        bool takesSource;
        bool takesArguments;

        if (count == 0)
        {
            takesSource = false;
            takesArguments = false;
        }
        else if (firstIsSource && count == arity + 1)
        {
            takesSource = true;
            takesArguments = arity > 0;
        }
        else if (count == arity)
        {
            // One node-typed parameter against one non-object argument can only mean the source.
            takesSource = firstIsSource && arity == 1 && shape.ArgumentTypes[0] != Variant.Type.Object;
            takesArguments = !takesSource;
        }
        else if (firstIsSource && count == 1)
        {
            takesSource = true;
            takesArguments = false;
        }
        else
        {
            var expected = OnInstance
                ? $"no parameters, or the signal's {arity} argument(s)"
                : $"no parameters, the source node, the signal's {arity} argument(s), or the source followed by them";
            LiturgyReport.AddMethodError(MethodName, HookName, $"Signal '{Signal}' has {arity} argument(s); expected {expected}.");
            return null;
        }

        var converters = new Func<Variant, ArgumentValue>[takesArguments ? arity : 0];
        var offset = takesSource ? 1 : 0;
        for (var i = 0; i < converters.Length; i++) converters[i] = VariantConversion.For(parameters[i + offset].ParameterType);

        return new Binding(this, takesSource ? parameters[0].ParameterType : null, converters, count);
    }

    private static bool IsSourceType(Type type) => typeof(Node).IsAssignableFrom(type) || type.IsInterface;

    private void ReportFailure(Exception exception)
    {
        if (_reportedFailure) return;
        _reportedFailure = true;
        GD.PushError($"[Liturgy] {HookName} '{MethodName}' couldn't take signal '{Signal}': {exception.Message}");
    }

    internal sealed class Binding(SignalRite rite, Type? sourceType, Func<Variant, ArgumentValue>[] converters, int parameterCount)
    {
        public SignalRite Rite { get; } = rite;

        /// <summary>
        /// False when the method's source parameter rules this node out.
        /// </summary>
        public bool Accepts(Node node) => sourceType is null || sourceType.IsInstanceOfType(node);

        public void Invoke(Node node, Variant[] arguments)
        {
            var values = parameterCount == 0 ? [] : new object?[parameterCount];
            var offset = 0;

            if (sourceType is not null) values[offset++] = node;

            try
            {
                for (var i = 0; i < converters.Length; i++)
                {
                    var (accepted, value) = converters[i](arguments[i]);
                    if (!accepted) return;
                    values[offset + i] = value;
                }
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
            {
                Rite.ReportFailure(exception);
                return;
            }

            try { RiteProbe.Run(Rite.Method, node, (Rite, node, values), static call => call.Rite.Method.Invoke(call.Rite.OnInstance ? call.node : null, BindingFlags.DoNotWrapExceptions, null, call.values, CultureInfo.InvariantCulture)); }
            catch (Exception exception) { RiteCall.Report(exception, Rite.MethodName); }
        }
    }
}

/// <summary>
/// A signal's argument count and types, as Godot declares them.
/// </summary>
internal readonly record struct SignalShape(int Arity, Variant.Type[] ArgumentTypes)
{
    public bool Equals(SignalShape other) => Arity == other.Arity && ArgumentTypes.AsSpan().SequenceEqual(other.ArgumentTypes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Arity);
        foreach (var type in ArgumentTypes) hash.Add(type);
        return hash.ToHashCode();
    }

    private static readonly Dictionary<(string Key, StringName Signal), SignalShape> _cache = [];

    /// <summary>
    /// The shape of a signal on an object, or null if the object doesn't have it.
    /// </summary>
    public static SignalShape? Of(GodotObject emitter, StringName signal)
    {
        if (!emitter.HasSignal(signal)) return null;

        // Engine signals are fixed per class, and a C# script's signals per type; GDScript and user signals aren't, so they're read each time.
        var className = emitter.GetClass();
        if (ClassDB.ClassHasSignal(className, signal)) return Cached($"class:{className}", signal, () => FromInfo(ClassDB.ClassGetSignal(className, signal)));

        var type = emitter.GetType();
        if (type.Assembly != typeof(GodotObject).Assembly) return Cached($"type:{type.AssemblyQualifiedName}", signal, () => FromList(emitter, signal));

        return FromList(emitter, signal);
    }

    private static SignalShape Cached(string key, StringName signal, Func<SignalShape> read)
    {
        if (_cache.TryGetValue((key, signal), out var shape)) return shape;
        shape = read();
        _cache[(key, signal)] = shape;
        return shape;
    }

    private static SignalShape FromList(GodotObject emitter, StringName signal)
    {
        var name = signal.ToString();
        foreach (var info in emitter.GetSignalList())
        {
            if (info["name"].AsString() == name) return FromInfo(info);
        }

        return new SignalShape(0, []);
    }

    private static SignalShape FromInfo(Godot.Collections.Dictionary info)
    {
        var arguments = info["args"].AsGodotArray();
        var types = new Variant.Type[arguments.Count];
        for (var i = 0; i < types.Length; i++) types[i] = (Variant.Type)arguments[i].AsGodotDictionary()["type"].AsInt32();
        return new SignalShape(types.Length, types);
    }
}

/// <summary>
/// A converted signal argument. <c>Accepted</c> is false when an object argument isn't of the parameter's type, which skips the call.
/// </summary>
internal readonly record struct ArgumentValue(bool Accepted, object? Value);

internal static class VariantConversion
{
    private static readonly MethodInfo AS_METHOD = typeof(VariantConversion).GetMethod(nameof(As), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Func<Variant, ArgumentValue> For(Type type)
    {
        if (type == typeof(Variant)) return variant => new ArgumentValue(true, variant);

        if (typeof(GodotObject).IsAssignableFrom(type) || type.IsInterface)
        {
            return variant =>
            {
                var instance = variant.VariantType == Variant.Type.Object ? variant.AsGodotObject() : null;
                return instance is null ? new ArgumentValue(true, null) : new ArgumentValue(type.IsInstanceOfType(instance), instance);
            };
        }

        var fallback = new Lazy<Func<Variant, object?>>(() => AS_METHOD.MakeGenericMethod(type).CreateDelegate<Func<Variant, object?>>());

        return variant =>
        {
            var value = variant.Obj;

            if (value is null) return new ArgumentValue(true, type.IsValueType ? Activator.CreateInstance(type) : null);
            if (type.IsInstanceOfType(value)) return new ArgumentValue(true, value);
            if (type == typeof(string)) return new ArgumentValue(true, value.ToString());
            if (type.IsEnum) return new ArgumentValue(true, Enum.ToObject(type, Convert.ToInt64(value, CultureInfo.InvariantCulture)));
            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(type)) return new ArgumentValue(true, Convert.ChangeType(value, type, CultureInfo.InvariantCulture));

            return new ArgumentValue(true, fallback.Value(variant));
        };
    }

    private static object? As<[MustBeVariant] T>(Variant variant) => variant.As<T>();
}
