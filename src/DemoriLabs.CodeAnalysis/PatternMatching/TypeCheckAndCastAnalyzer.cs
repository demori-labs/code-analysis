using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeCheckAndCastAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseDeclarationPatternInsteadOfCast,
        title: "Use declaration pattern instead of type check and cast",
        messageFormat: "Use 'is {0} varName' instead of type check followed by cast",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When checking a type with 'is' and then casting in the body, use a declaration pattern instead."
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
                analysisContext => AnalyzeIsExpression(analysisContext, expressionType),
                SyntaxKind.IsExpression
            );
        });
    }

    private static void AnalyzeIsExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var isExpression = (BinaryExpressionSyntax)context.Node;

        var ifStatement = FindEnclosingIfCondition(isExpression);
        if (ifStatement is null)
            return;

        var checkedType = isExpression.Right;
        var checkedExpression = isExpression.Left;

        var ifBody = ifStatement.Statement;

        var hasCast = ContainsCastOfSameType(ifBody, checkedExpression, checkedType);
        if (hasCast is false)
            return;

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                isExpression,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var typeName = checkedType.ToString();
        context.ReportDiagnostic(Diagnostic.Create(Rule, isExpression.GetLocation(), typeName));
    }

    private static IfStatementSyntax? FindEnclosingIfCondition(BinaryExpressionSyntax isExpression)
    {
        var current = isExpression.Parent;
        while (
            current
                is ParenthesizedExpressionSyntax
                    or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression }
        )
        {
            current = current.Parent;
        }

        return current as IfStatementSyntax;
    }

    private static bool ContainsCastOfSameType(
        StatementSyntax body,
        ExpressionSyntax checkedExpression,
        ExpressionSyntax checkedType
    )
    {
        var checkedExpressionText = checkedExpression.WithoutTrivia().ToString();
        var checkedTypeText = checkedType.WithoutTrivia().ToString();

        foreach (var cast in body.DescendantNodes().OfType<CastExpressionSyntax>())
        {
            var castTypeText = cast.Type.WithoutTrivia().ToString();
            var castExpressionText = cast.Expression.WithoutTrivia().ToString();

            if (
                string.Equals(castTypeText, checkedTypeText, StringComparison.Ordinal)
                && string.Equals(castExpressionText, checkedExpressionText, StringComparison.Ordinal)
            )
            {
                return true;
            }
        }

        return false;
    }
}
