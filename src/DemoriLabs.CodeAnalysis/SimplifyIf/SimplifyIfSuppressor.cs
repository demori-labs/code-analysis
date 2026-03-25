using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.SimplifyIf;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SimplifyIfSuppressor : DiagnosticSuppressor
{
    // ReSharper disable InconsistentNaming
    private static readonly SuppressionDescriptor SuppressIDE0045 = new(
        id: "SPR3011",
        suppressedDiagnosticId: "IDE0045",
        justification: "DL3011 provides conditional assignment simplification with Expression tree awareness."
    );

    private static readonly SuppressionDescriptor SuppressIDE0046 = new(
        id: "SPR3010",
        suppressedDiagnosticId: "IDE0046",
        justification: "DL3010 provides conditional return simplification with Expression tree awareness."
    );

    // ReSharper restore InconsistentNaming

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [SuppressIDE0045, SuppressIDE0046];

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var descriptor = diagnostic.Id switch
            {
                "IDE0045" => SuppressIDE0045,
                "IDE0046" => SuppressIDE0046,
                _ => null,
            };

            if (descriptor is not null)
            {
                context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
            }
        }
    }
}
