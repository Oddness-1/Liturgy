using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Liturgy.Generators;

/// <summary>
/// Silences warnings that are wrong about Liturgy's members: an <c>[Rite.AssignNode]</c> field is assigned (by Liturgy,
/// at startup), must not be made readonly (Liturgy couldn't assign it), and a rite method is used (Liturgy calls it).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RiteMemberSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor NEVER_ASSIGNED = new("LITS001", "CS0649", "Liturgy assigns [Rite.AssignNode] members when /root is ready.");
    private static readonly SuppressionDescriptor MAKE_READONLY = new("LITS002", "IDE0044", "Liturgy assigns [Rite.AssignNode] members, so they can't be readonly.");
    private static readonly SuppressionDescriptor UNUSED_MEMBER = new("LITS003", "IDE0051", "Liturgy calls rite methods and assigns [Rite.AssignNode] members.");
    private static readonly SuppressionDescriptor UNREAD_MEMBER = new("LITS004", "IDE0052", "Liturgy assigns [Rite.AssignNode] members.");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = [NEVER_ASSIGNED, MAKE_READONLY, UNUSED_MEMBER, UNREAD_MEMBER];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var descriptor = SupportedSuppressions.FirstOrDefault(suppression => suppression.SuppressedDiagnosticId == diagnostic.Id);
            if (descriptor is null || diagnostic.Location.SourceTree is not { } tree) continue;

            var model = context.GetSemanticModel(tree);
            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
            var symbol = model.GetDeclaredSymbol(node, context.CancellationToken);

            if (symbol is not null && CarriesRite(symbol)) context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
        }
    }

    private static bool CarriesRite(ISymbol symbol) => symbol.GetAttributes().Any(attribute =>
        attribute.AttributeClass is { ContainingType: { Name: "Rite", ContainingNamespace: { Name: "Liturgy", ContainingNamespace.IsGlobalNamespace: true } } } attributeClass &&
        attributeClass.Name != "SuppressWarnings");
}
