using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.StringEquals;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseStringEqualsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseStringEqualsWithComparison,
        title: "Use string.Equals with StringComparison",
        messageFormat: "Use '{0}' instead of '{1}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer string.Equals with an explicit StringComparison over == and != operators on strings."
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
                analysisContext => AnalyzeBinaryExpression(analysisContext, expressionType),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeIsPatternExpression(analysisContext, expressionType),
                SyntaxKind.IsPatternExpression
            );
        });
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        var left = binaryExpression.Left;
        var right = binaryExpression.Right;

        if (IsNullOrDefault(left) || IsNullOrDefault(right))
            return;

        // Skip empty string comparisons — DL5002 suggests string.IsNullOrEmpty instead
        if (IsEmptyString(left) || IsEmptyString(right))
            return;

        var leftType = context.SemanticModel.GetTypeInfo(left, context.CancellationToken).Type;
        var rightType = context.SemanticModel.GetTypeInfo(right, context.CancellationToken).Type;

        if (IsStringType(leftType) is false || IsStringType(rightType) is false)
            return;

        var leftConstant = context.SemanticModel.GetConstantValue(left, context.CancellationToken);
        var rightConstant = context.SemanticModel.GetConstantValue(right, context.CancellationToken);

        if (leftConstant.HasValue && rightConstant.HasValue)
            return;

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                binaryExpression,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var leftText = left.WithoutTrivia().ToFullString();
        var rightText = right.WithoutTrivia().ToFullString();
        var isEquals = binaryExpression.IsKind(SyntaxKind.EqualsExpression);

        var suggestion = isEquals
            ? $"string.Equals({leftText}, {rightText}, StringComparison.Ordinal)"
            : $"string.Equals({leftText}, {rightText}, StringComparison.Ordinal) is false";

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, binaryExpression.GetLocation(), suggestion, binaryExpression.ToString())
        );
    }

    private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var isPattern = (IsPatternExpressionSyntax)context.Node;

        string? literalText;
        bool isNegated;

        switch (isPattern.Pattern)
        {
            case ConstantPatternSyntax
            {
                Expression: LiteralExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.StringLiteralExpression,
                    Token.ValueText.Length: > 0,
                } literal,
            }:
                literalText = literal.ToString();
                isNegated = false;
                break;
            case UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax
                {
                    Expression: LiteralExpressionSyntax
                    {
                        RawKind: (int)SyntaxKind.StringLiteralExpression,
                        Token.ValueText.Length: > 0,
                    } innerLiteral,
                },
            }:
                literalText = innerLiteral.ToString();
                isNegated = true;
                break;
            default:
                return;
        }

        var exprType = context.SemanticModel.GetTypeInfo(isPattern.Expression, context.CancellationToken).Type;
        if (IsStringType(exprType) is false)
            return;

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                isPattern,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var exprText = isPattern.Expression.WithoutTrivia().ToFullString();

        var suggestion = isNegated
            ? $"string.Equals({exprText}, {literalText}, StringComparison.Ordinal) is false"
            : $"string.Equals({exprText}, {literalText}, StringComparison.Ordinal)";

        context.ReportDiagnostic(Diagnostic.Create(Rule, isPattern.GetLocation(), suggestion, isPattern.ToString()));
    }

    private static bool IsStringType(ITypeSymbol? type)
    {
        return type?.SpecialType is SpecialType.System_String;
    }

    private static bool IsNullOrDefault(ExpressionSyntax expr)
    {
        return expr.IsKind(SyntaxKind.NullLiteralExpression)
            || expr.IsKind(SyntaxKind.DefaultLiteralExpression)
            || expr is DefaultExpressionSyntax;
    }

    private static bool IsEmptyString(ExpressionSyntax expr)
    {
        return expr
            is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression, Token.ValueText.Length: 0 };
    }
}
