using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Namespaces;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamespacesSuppressor : DiagnosticSuppressor
{
    // ReSharper disable InconsistentNaming
    private static readonly SuppressionDescriptor SuppressIDE0130 = new(
        id: "SPR3018",
        suppressedDiagnosticId: "IDE0130",
        justification: "DL3018 provides namespace-to-folder matching that works during dotnet build without EnforceCodeStyleInBuild."
    );

    private static readonly SuppressionDescriptor SuppressIDE0161 = new(
        id: "SPR3019",
        suppressedDiagnosticId: "IDE0161",
        justification: "DL3019 provides file-scoped namespace conversion that works during dotnet build without EnforceCodeStyleInBuild."
    );

    // ReSharper restore InconsistentNaming

    /// <inheritdoc />
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [SuppressIDE0130, SuppressIDE0161];

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
            "IDE0130" => SuppressIDE0130,
            "IDE0161" => SuppressIDE0161,
            _ => null,
        };
    }
}
