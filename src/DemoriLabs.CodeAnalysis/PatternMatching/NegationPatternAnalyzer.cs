using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NegationPatternAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseNegationPattern,
        title: "Use 'is false' or 'is not' instead of '!'",
        messageFormat: "Use '{0}' instead of '{1}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer 'is false' over the '!' operator for boolean negation, and 'is not' for negated type checks or patterns."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var expressionType = compilationContext.Compilation.GetTypeByMetadataName(
                "System.Linq.Expressions.Expression`1"
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeLogicalNot(analysisContext, expressionType),
                SyntaxKind.LogicalNotExpression
            );
        });
    }

    private static void AnalyzeLogicalNot(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var prefixUnary = (PrefixUnaryExpressionSyntax)context.Node;
        var operand = prefixUnary.Operand;

        var unwrapped = operand;
        while (unwrapped is ParenthesizedExpressionSyntax paren)
            unwrapped = paren.Expression;

        switch (unwrapped)
        {
            // !(a && b) or !(a || b) — De Morgan's rewrite, skip unless inner is an is-expression/pattern
            case BinaryExpressionSyntax binary
                when binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression:
            case IsPatternExpressionSyntax
            or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression }
                when expressionType is not null
                    && ExpressionTreeHelper.IsInsideExpressionTree(
                        prefixUnary,
                        context.SemanticModel,
                        expressionType,
                        context.CancellationToken
                    ):
                return;
            case IsPatternExpressionSyntax or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression }:
            {
                var suggestion = BuildIsNotSuggestion(unwrapped);
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, prefixUnary.GetLocation(), suggestion, prefixUnary.ToString())
                );
                return;
            }
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(operand, context.CancellationToken);

        if (typeInfo.Type?.SpecialType is not SpecialType.System_Boolean)
            return;

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                prefixUnary,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var operandText = operand.ToString();
        var suggestionText = $"{operandText} is false";
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, prefixUnary.GetLocation(), suggestionText, prefixUnary.ToString())
        );
    }

    private static string BuildIsNotSuggestion(ExpressionSyntax unwrapped)
    {
        return unwrapped switch
        {
            IsPatternExpressionSyntax isPattern => $"{isPattern.Expression} is not {isPattern.Pattern}",
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpr =>
                $"{isExpr.Left} is not {isExpr.Right}",
            _ => $"{unwrapped} is false",
        };
    }
}
