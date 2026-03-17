using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.CognitiveComplexity;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CognitiveComplexityAnalyzer : DiagnosticAnalyzer
{
    internal const int DefaultModerateThreshold = 5;
    internal const int DefaultElevatedThreshold = 12;

    private const string ModerateThresholdOptionKey =
        "dotnet_diagnostic.DL4001.cognitive_complexity_moderate_threshold";

    private const string ElevatedThresholdOptionKey =
        "dotnet_diagnostic.DL4001.cognitive_complexity_elevated_threshold";

    private static readonly DiagnosticDescriptor ModerateRule = new(
        RuleIdentifiers.MethodHasModerateCognitiveComplexity,
        title: "Method has moderate cognitive complexity",
        messageFormat: "Method '{0}' has a cognitive complexity of {1} (moderate threshold: {2})",
        RuleCategories.Complexity,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Methods with moderate cognitive complexity may become hard to understand. Consider simplifying the control flow."
    );

    private static readonly DiagnosticDescriptor ElevatedRule = new(
        RuleIdentifiers.MethodHasElevatedCognitiveComplexity,
        title: "Method has elevated cognitive complexity",
        messageFormat: "Method '{0}' has a cognitive complexity of {1} (elevated threshold: {2})",
        RuleCategories.Complexity,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Methods with elevated cognitive complexity are hard to understand and maintain. Consider breaking them into smaller methods."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ModerateRule, ElevatedRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodSyntax = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax, context.CancellationToken);
        if (methodSymbol is null)
        {
            return;
        }

        if (IsSuppressed(methodSymbol))
        {
            return;
        }

        var (moderateThreshold, elevatedThreshold) = ResolveThresholds(methodSymbol, context);

        var body = (SyntaxNode?)methodSyntax.Body ?? methodSyntax.ExpressionBody;
        if (body is null)
        {
            return;
        }

        var complexity = CognitiveComplexityCalculator.Calculate(body, context.SemanticModel, methodSymbol);

        if (complexity > elevatedThreshold)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    ElevatedRule,
                    methodSymbol.Locations[0],
                    methodSymbol.Name,
                    complexity,
                    elevatedThreshold
                )
            );
        }
        else if (complexity > moderateThreshold)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    ModerateRule,
                    methodSymbol.Locations[0],
                    methodSymbol.Name,
                    complexity,
                    moderateThreshold
                )
            );
        }
    }

    private static bool IsSuppressed(IMethodSymbol method)
    {
        return AnnotationAttributes.HasSuppressCognitiveComplexityAttribute(method)
            || AnnotationAttributes.HasSuppressCognitiveComplexityAttribute(method.ContainingType);
    }

    private static (int moderate, int elevated) ResolveThresholds(
        IMethodSymbol method,
        SyntaxNodeAnalysisContext context
    )
    {
        var thresholdAttribute =
            AnnotationAttributes.GetCognitiveComplexityThresholdAttribute(method)
            ?? AnnotationAttributes.GetCognitiveComplexityThresholdAttribute(method.ContainingType);

        if (thresholdAttribute is null)
            return GetThresholdsFromOptions(context);

        var moderate = thresholdAttribute.ConstructorArguments[0].Value as int? ?? DefaultModerateThreshold;
        var elevated = thresholdAttribute.ConstructorArguments[1].Value as int? ?? DefaultElevatedThreshold;

        return (moderate, elevated);
    }

    private static (int moderate, int elevated) GetThresholdsFromOptions(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        var moderate = DefaultModerateThreshold;
        if (
            options.TryGetValue(ModerateThresholdOptionKey, out var moderateValue)
            && int.TryParse(moderateValue, out var parsedModerate)
        )
        {
            moderate = parsedModerate;
        }

        var elevated = DefaultElevatedThreshold;
        if (
            options.TryGetValue(ElevatedThresholdOptionKey, out var elevatedValue)
            && int.TryParse(elevatedValue, out var parsedElevated)
        )
        {
            elevated = parsedElevated;
        }

        return (moderate, elevated);
    }
}
