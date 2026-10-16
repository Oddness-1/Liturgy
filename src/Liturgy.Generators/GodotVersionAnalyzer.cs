using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Liturgy.Generators;

/// <summary>
/// Warns when a project uses a newer Godot than Liturgy was tested with. Liturgy attaches by having Godot build an
/// attribute while it loads scripts; if a Godot release stops doing that, Liturgy silently never starts, and nothing
/// of Liturgy's runs to say so. This is the only place that can.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GodotVersionAnalyzer : DiagnosticAnalyzer
{
    private const string GODOT_ASSEMBLY = "GodotSharp";
    private static readonly Version TESTED_GODOT = new(4, 7);

    internal static readonly DiagnosticDescriptor UNTESTED_GODOT = new(
        "LIT007", "Godot version not tested with Liturgy",
        "Liturgy was tested with Godot {0}, and this project uses Godot {1}; run the game once and check that /root/Liturgy exists before relying on rites",
        "Liturgy", DiagnosticSeverity.Warning, isEnabledByDefault: true, customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UNTESTED_GODOT];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(compilation =>
        {
            if (compilation.Compilation.GetTypeByMetadataName("Liturgy.Rite") is null) return;
            if (compilation.Compilation.ReferencedAssemblyNames.FirstOrDefault(static identity => identity.Name == GODOT_ASSEMBLY) is not { } godot) return;

            var used = new Version(godot.Version.Major, godot.Version.Minor);
            if (used <= TESTED_GODOT) return;

            compilation.ReportDiagnostic(Diagnostic.Create(UNTESTED_GODOT, Location.None, TESTED_GODOT, used));
        });
    }
}
