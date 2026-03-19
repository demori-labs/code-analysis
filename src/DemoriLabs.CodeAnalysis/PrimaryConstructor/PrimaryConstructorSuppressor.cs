using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PrimaryConstructor;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrimaryConstructorSuppressor : DiagnosticSuppressor
{
    // ReSharper disable once InconsistentNaming
    private static readonly SuppressionDescriptor SuppressIDE0290 = new(
        id: "SPR1005",
        suppressedDiagnosticId: "IDE0290",
        justification: "DL1005 provides a primary constructor suggestion with [ReadOnly] attribute support."
    );

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [SuppressIDE0290];

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            context.ReportSuppression(Suppression.Create(SuppressIDE0290, diagnostic));
        }
    }
}
