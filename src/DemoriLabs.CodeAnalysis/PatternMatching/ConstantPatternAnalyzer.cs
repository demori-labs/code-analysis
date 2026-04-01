using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantPatternAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseConstantPattern,
        title: "Use constant pattern",
        messageFormat: "Use '{0}' instead of '{1}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer pattern matching with 'is' over equality operators when comparing against constants."
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
                analysisContext =>
                {
                    AnalyzeIsTrueFalsePattern(analysisContext, expressionType);
                    AnalyzeIsDefaultPattern(analysisContext, expressionType);
                },
                SyntaxKind.IsPatternExpression
            );
        });
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        if (
            LogicalPatternAnalyzer.IsLeafOfLogicalPatternChain(
                binaryExpression,
                context.SemanticModel,
                context.CancellationToken
            )
        )
        {
            return;
        }

        // Skip inner comparison when wrapped: (x == null) == false, (x != null) == true, etc.
        if (IsWrappedInOuterComparison(binaryExpression))
        {
            return;
        }

        var left = binaryExpression.Left;
        var right = binaryExpression.Right;

        var leftIsConstant = IsConstantExpression(left, context.SemanticModel, context.CancellationToken);
        var rightIsConstant = IsConstantExpression(right, context.SemanticModel, context.CancellationToken);

        if (leftIsConstant == rightIsConstant)
            return;

        var constant = leftIsConstant ? left : right;

        // Skip non-null string comparisons — a dedicated analyser will suggest string.Equals with StringComparison
        if (
            constant is LiteralExpressionSyntax
            {
                RawKind: (int)SyntaxKind.StringLiteralExpression or (int)SyntaxKind.Utf8StringLiteralExpression,
            }
        )
        {
            return;
        }

        // Skip str.Length == 0 — DL5002 suggests string.IsNullOrEmpty instead
        var variable2 = leftIsConstant ? right : left;
        if (IsStringLengthZeroComparison(variable2, constant, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

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

        var isEquals = binaryExpression.IsKind(SyntaxKind.EqualsExpression);
        var variable = leftIsConstant ? right : left;

        var constantText = DefaultValueHelper.IsDefaultExpression(constant)
            ? DefaultValueHelper.ResolveDefaultPatternText(variable, context.SemanticModel, context.CancellationToken)
            : constant.ToString();
        var variableText = variable.ToString();

        var suggestion = isEquals ? $"{variableText} is {constantText}" : $"{variableText} is not {constantText}";

        var originalText = binaryExpression.ToString();

        context.ReportDiagnostic(Diagnostic.Create(Rule, binaryExpression.GetLocation(), suggestion, originalText));
    }

    private static void AnalyzeIsTrueFalsePattern(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var isPattern = (IsPatternExpressionSyntax)context.Node;

        // Handle: `expr is true`, `expr is false`, `expr is not true`, `expr is not false`
        bool isFalsePattern;
        switch (isPattern.Pattern)
        {
            case ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression }:
                isFalsePattern = false;
                break;
            case ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.FalseLiteralExpression }:
                isFalsePattern = true;
                break;
            case UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression },
            }:
                // is not true == is false
                isFalsePattern = true;
                break;
            case UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.FalseLiteralExpression },
            }:
                // is not false == is true
                isFalsePattern = false;
                break;
            default:
                return;
        }

        // Skip if this is-pattern is itself wrapped in an outer comparison or negation
        if (IsWrappedInOuterComparison(isPattern))
        {
            return;
        }

        // The expression must be a parenthesized comparison, is-pattern, or negated HasValue
        var unwrapped = isPattern.Expression;
        while (unwrapped is ParenthesizedExpressionSyntax paren)
            unwrapped = paren.Expression;

        switch (unwrapped)
        {
            case BinaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression,
            } comparison:
            {
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

                var suggestion = BuildComparisonSimplification(comparison, isFalsePattern);
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, isPattern.GetLocation(), suggestion, isPattern.ToString())
                );
                break;
            }
            case IsPatternExpressionSyntax innerIsPattern:
            {
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

                var suggestion = BuildIsPatternSimplification(innerIsPattern, isFalsePattern);
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, isPattern.GetLocation(), suggestion, isPattern.ToString())
                );
                break;
            }
            // !(id.HasValue) is true/false/not true/not false
            case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation:
            {
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

                var suggestion = BuildNegationSimplification(
                    negation,
                    isFalsePattern,
                    context.SemanticModel,
                    context.CancellationToken
                );
                if (suggestion is not null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(Rule, isPattern.GetLocation(), suggestion, isPattern.ToString())
                    );
                }

                break;
            }
        }
    }

    private static void AnalyzeIsDefaultPattern(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var isPattern = (IsPatternExpressionSyntax)context.Node;

        bool isNegated;
        switch (isPattern.Pattern)
        {
            case ConstantPatternSyntax { Expression: var expr } when DefaultValueHelper.IsDefaultExpression(expr):
                isNegated = false;
                break;
            case UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax { Expression: var expr },
            } when DefaultValueHelper.IsDefaultExpression(expr):
                isNegated = true;
                break;
            default:
                return;
        }

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

        var variableText = isPattern.Expression.WithoutTrivia().ToFullString();
        var resolvedDefault = DefaultValueHelper.ResolveDefaultPatternText(
            isPattern.Expression,
            context.SemanticModel,
            context.CancellationToken
        );

        var suggestion = isNegated
            ? $"{variableText} is not {resolvedDefault}"
            : $"{variableText} is {resolvedDefault}";

        context.ReportDiagnostic(Diagnostic.Create(Rule, isPattern.GetLocation(), suggestion, isPattern.ToString()));
    }

    private static string BuildComparisonSimplification(BinaryExpressionSyntax comparison, bool negate)
    {
        var leftIsLiteral =
            comparison.Left
            is LiteralExpressionSyntax
                or PrefixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.UnaryMinusExpression,
                    Operand: LiteralExpressionSyntax,
                }
                or MemberAccessExpressionSyntax;
        var variable = leftIsLiteral ? comparison.Right : comparison.Left;
        var constant = leftIsLiteral ? comparison.Left : comparison.Right;

        var innerIsEquals = comparison.IsKind(SyntaxKind.EqualsExpression);
        var resultIsPositive = innerIsEquals != negate;

        var variableText = variable.WithoutTrivia().ToFullString();
        var constantText = constant.WithoutTrivia().ToFullString();

        return resultIsPositive ? $"{variableText} is {constantText}" : $"{variableText} is not {constantText}";
    }

    private static string? BuildNegationSimplification(
        PrefixUnaryExpressionSyntax negation,
        bool isFalsePattern,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var inner = negation.Operand;
        while (inner is ParenthesizedExpressionSyntax paren)
            inner = paren.Expression;

        // !id.HasValue on Nullable<T>: negation means "is null", so:
        // !(id.HasValue) is true  → isFalse=false → id is null
        // !(id.HasValue) is false → isFalse=true  → id is not null
        // !(id.HasValue) is not true  → isFalse=true  → id is not null
        // !(id.HasValue) is not false → isFalse=false → id is null
        if (
            inner is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } hasValueAccess
            && semanticModel.GetTypeInfo(hasValueAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T
        )
        {
            var ownerText = hasValueAccess.Expression.WithoutTrivia().ToFullString();
            return isFalsePattern ? $"{ownerText} is not null" : $"{ownerText} is null";
        }

        // Generic !expr: resolve based on boolean semantics
        var operandText = negation.Operand.WithoutTrivia().ToFullString();
        // !(expr) is true  → expr is false
        // !(expr) is false → expr is true (double negation)
        return isFalsePattern ? $"{operandText} is true" : $"{operandText} is false";
    }

    private static string BuildIsPatternSimplification(IsPatternExpressionSyntax isPattern, bool negate)
    {
        var expr = isPattern.Expression.WithoutTrivia().ToFullString();

        if (negate)
        {
            if (isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } notPattern)
            {
                return $"{expr} is {notPattern.Pattern.WithoutTrivia().ToFullString()}";
            }

            return $"{expr} is not {isPattern.Pattern.WithoutTrivia().ToFullString()}";
        }

        return $"{expr} is {isPattern.Pattern.WithoutTrivia().ToFullString()}";
    }

    private static bool IsWrappedInOuterComparison(SyntaxNode inner)
    {
        SyntaxNode? current = inner.Parent;
        while (current is ParenthesizedExpressionSyntax)
            current = current.Parent;

        return current switch
        {
            // (x == null) == false, (x != null) == true, etc.
            BinaryExpressionSyntax outer
                when outer.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression => true,
            // !(x == null), !(x != null)
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } => true,
            // (x == null) is false, (x != null) is true, is not true, is not false
            IsPatternExpressionSyntax
            {
                Pattern: ConstantPatternSyntax
                    {
                        Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression
                            or (int)SyntaxKind.FalseLiteralExpression
                    }
                    or UnaryPatternSyntax
                    {
                        RawKind: (int)SyntaxKind.NotPattern,
                        Pattern: ConstantPatternSyntax
                        {
                            Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression
                                or (int)SyntaxKind.FalseLiteralExpression
                        }
                    }
            } => true,
            _ => false,
        };
    }

    private static bool IsConstantExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        while (expression is ParenthesizedExpressionSyntax paren)
            expression = paren.Expression;

        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
            return true;

        switch (expression)
        {
            case LiteralExpressionSyntax:
            case PrefixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.UnaryMinusExpression,
                Operand: LiteralExpressionSyntax,
            }:
                return true;
            default:
            {
                var constantValue = semanticModel.GetConstantValue(expression, cancellationToken);
                return constantValue.HasValue;
            }
        }
    }

    private static bool IsStringLengthZeroComparison(
        ExpressionSyntax variable,
        ExpressionSyntax constant,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        if (constant is not LiteralExpressionSyntax { Token.ValueText: "0" })
            return false;

        if (variable is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" } memberAccess)
            return false;

        var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, ct).Type;
        return receiverType?.SpecialType is SpecialType.System_String;
    }
}
