using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.NullSimplification;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullSimplificationSuppressor : DiagnosticSuppressor
{
    // ReSharper disable InconsistentNaming
    private static readonly SuppressionDescriptor SuppressIDE0029 = new(
        id: "SPR3013",
        suppressedDiagnosticId: "IDE0029",
        justification: "DL3013 provides null-coalescing suggestion with Expression tree awareness."
    );

    private static readonly SuppressionDescriptor SuppressIDE0030 = new(
        id: "SPR3013b",
        suppressedDiagnosticId: "IDE0030",
        justification: "DL3013 provides null-coalescing suggestion with Expression tree awareness."
    );

    private static readonly SuppressionDescriptor SuppressIDE0074 = new(
        id: "SPR3014",
        suppressedDiagnosticId: "IDE0074",
        justification: "DL3014 provides null-coalescing assignment with Expression tree awareness."
    );

    // ReSharper restore InconsistentNaming

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        [SuppressIDE0029, SuppressIDE0030, SuppressIDE0074];

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var descriptor = diagnostic.Id switch
            {
                "IDE0029" => SuppressIDE0029,
                "IDE0030" => SuppressIDE0030,
                "IDE0074" => SuppressIDE0074,
                _ => null,
            };

            if (descriptor is not null)
            {
                context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
            }
        }
    }
}
