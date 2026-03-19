using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.ReadOnlyParameter;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuggestReadOnlyPrimaryConstructorParameterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.SuggestReadOnlyPrimaryConstructorParameter,
        title: "Primary constructor parameter should be [ReadOnly]",
        messageFormat: "Consider adding [ReadOnly] to primary constructor parameter '{0}'",
        RuleCategories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Primary constructor parameters on classes should be marked with [ReadOnly] to prevent accidental reassignment."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;

        if (parameter.Parent?.Parent is not ClassDeclarationSyntax)
            return;

        if (
            parameter.Modifiers.Any(m =>
                m.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword or SyntaxKind.InKeyword
            )
        )
        {
            return;
        }

        if (
            AnnotationAttributes.HasReadOnlyAttribute(
                context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)!
            )
        )
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, parameter.Identifier.GetLocation(), parameter.Identifier.Text)
        );
    }
}
