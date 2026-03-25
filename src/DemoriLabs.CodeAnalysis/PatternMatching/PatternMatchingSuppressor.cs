using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PatternMatchingSuppressor : DiagnosticSuppressor
{
    // ReSharper disable InconsistentNaming
    private static readonly SuppressionDescriptor SuppressIDE0041 = new(
        id: "SPR3003a",
        suppressedDiagnosticId: "IDE0041",
        justification: "DL3003 provides constant pattern analysis with Expression tree awareness."
    );

    private static readonly SuppressionDescriptor SuppressIDE0078 = new(
        id: "SPR3003b",
        suppressedDiagnosticId: "IDE0078",
        justification: "DL3003 and DL3005 provide broader constant and logical pattern analysis."
    );

    private static readonly SuppressionDescriptor SuppressIDE0083 = new(
        id: "SPR3004",
        suppressedDiagnosticId: "IDE0083",
        justification: "DL3004 provides negation pattern analysis with 'is false' support."
    );

    private static readonly SuppressionDescriptor SuppressIDE0020 = new(
        id: "SPR3006a",
        suppressedDiagnosticId: "IDE0020",
        justification: "DL3006 provides type check and cast pattern with Expression tree awareness."
    );

    private static readonly SuppressionDescriptor SuppressIDE0038 = new(
        id: "SPR3006b",
        suppressedDiagnosticId: "IDE0038",
        justification: "DL3006 provides type check and cast pattern with Expression tree awareness."
    );

    private static readonly SuppressionDescriptor SuppressIDE0019 = new(
        id: "SPR3007",
        suppressedDiagnosticId: "IDE0019",
        justification: "DL3007 provides as with null check pattern with Expression tree awareness."
    );

    // ReSharper restore InconsistentNaming

    private static readonly ImmutableArray<SuppressionDescriptor> AllSuppressions =
    [
        SuppressIDE0041,
        SuppressIDE0078,
        SuppressIDE0083,
        SuppressIDE0020,
        SuppressIDE0038,
        SuppressIDE0019,
    ];

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => AllSuppressions;

    /// <inheritdoc />
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var descriptor = GetDescriptor(diagnostic.Id);
            if (descriptor is not null)
            {
                context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
            }
        }
    }

    private static SuppressionDescriptor? GetDescriptor(string diagnosticId)
    {
        return diagnosticId switch
        {
            "IDE0041" => SuppressIDE0041,
            "IDE0078" => SuppressIDE0078,
            "IDE0083" => SuppressIDE0083,
            "IDE0020" => SuppressIDE0020,
            "IDE0038" => SuppressIDE0038,
            "IDE0019" => SuppressIDE0019,
            _ => null,
        };
    }
}
