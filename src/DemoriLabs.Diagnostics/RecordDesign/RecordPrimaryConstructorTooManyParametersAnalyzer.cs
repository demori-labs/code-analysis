using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.Diagnostics.RecordDesign;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordPrimaryConstructorTooManyParametersAnalyzer : DiagnosticAnalyzer
{
    private const int DefaultPositionalParametersThreshold = 4;

    private const string PositionalParametersThresholdOptionKey =
        "dotnet_diagnostic.DL1003.positional_parameters_threshold";

    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.RecordPrimaryConstructorTooManyParameters,
        title: "Record has too many positional parameters",
        messageFormat: "Record '{0}' has {1} positional parameters (threshold: {2}). Consider using explicit required properties instead.",
        RuleCategories.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Records with many positional parameters become hard to read and maintain. Declare properties explicitly as 'public required <type> <Name> { get; init; }' instead."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeRecord,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration
        );
    }

    private static void AnalyzeRecord(SyntaxNodeAnalysisContext context)
    {
        var record = (RecordDeclarationSyntax)context.Node;

        if (record.ParameterList is not { Parameters.Count: > 0 } parameterList)
            return;

        var parameterCount = parameterList.Parameters.Count;
        var threshold = GetThreshold(context);

        if (parameterCount > threshold)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    record.Identifier.GetLocation(),
                    record.Identifier.Text,
                    parameterCount,
                    threshold
                )
            );
        }
    }

    private static int GetThreshold(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        if (
            options.TryGetValue(PositionalParametersThresholdOptionKey, out var value)
            && int.TryParse(value, out var parsed)
        )
        {
            return parsed;
        }

        return DefaultPositionalParametersThreshold;
    }
}
