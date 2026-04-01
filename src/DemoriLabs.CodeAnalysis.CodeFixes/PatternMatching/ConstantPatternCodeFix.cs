using System.Collections.Immutable;
using System.Composition;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class ConstantPatternCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseConstantPattern];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: false);

        while (node is not null && IsFixableNode(node) is false)
        {
            node = node.Parent;
        }

        if (node is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use constant pattern",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(ConstantPatternCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null)
            return document;

        string replacementText;

        if (
            node is IsPatternExpressionSyntax isPatternNode
            && TryFixIsDefaultPattern(isPatternNode, semanticModel, ct, out var defaultFix)
        )
        {
            replacementText = defaultFix;
        }
        else if (node is IsPatternExpressionSyntax isTrueFalseNode)
        {
            replacementText = FixIsTrueFalsePattern(isTrueFalseNode, semanticModel, ct);
        }
        else if (node is BinaryExpressionSyntax binaryExpression)
        {
            replacementText = FixBinaryExpression(binaryExpression, semanticModel, ct);
        }
        else
        {
            return document;
        }

        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(node, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsFixableNode(SyntaxNode node)
    {
        return node
            is BinaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression,
                }
                or IsPatternExpressionSyntax
                {
                    Pattern: ConstantPatternSyntax
                        {
                            Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression
                                or (int)SyntaxKind.FalseLiteralExpression
                                or (int)SyntaxKind.DefaultLiteralExpression,
                        }
                        or ConstantPatternSyntax { Expression: DefaultExpressionSyntax }
                        or UnaryPatternSyntax
                        {
                            RawKind: (int)SyntaxKind.NotPattern,
                            Pattern: ConstantPatternSyntax
                                {
                                    Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression
                                        or (int)SyntaxKind.FalseLiteralExpression
                                        or (int)SyntaxKind.DefaultLiteralExpression,
                                }
                                or ConstantPatternSyntax { Expression: DefaultExpressionSyntax },
                        },
                };
    }

    private static bool TryFixIsDefaultPattern(
        IsPatternExpressionSyntax isPattern,
        SemanticModel semanticModel,
        CancellationToken ct,
        out string replacement
    )
    {
        replacement = "";

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
                return false;
        }

        var variableText = isPattern.Expression.WithoutTrivia().ToFullString();
        var resolvedDefault = DefaultValueHelper.ResolveDefaultPatternText(isPattern.Expression, semanticModel, ct);

        replacement = isNegated ? $"{variableText} is not {resolvedDefault}" : $"{variableText} is {resolvedDefault}";

        return true;
    }

    private static string FixIsTrueFalsePattern(
        IsPatternExpressionSyntax isPattern,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        bool isFalse;
        switch (isPattern.Pattern)
        {
            case ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.FalseLiteralExpression }:
                isFalse = true;
                break;
            case ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression }:
                isFalse = false;
                break;
            case UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression },
            }:
                isFalse = true;
                break;
            case UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.FalseLiteralExpression },
            }:
                isFalse = false;
                break;
            default:
                return isPattern.ToString();
        }

        var unwrapped = isPattern.Expression;
        while (unwrapped is ParenthesizedExpressionSyntax paren)
            unwrapped = paren.Expression;

        return unwrapped switch
        {
            BinaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression,
            } comparison => BuildComparisonFix(comparison, isFalse, semanticModel, ct),
            IsPatternExpressionSyntax innerIs => BuildIsPatternFix(innerIs, isFalse, semanticModel, ct),
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation => BuildNegationFix(
                negation,
                isFalse,
                semanticModel,
                ct
            ),
            _ => isFalse
                ? $"{isPattern.Expression.WithoutTrivia().ToFullString()} is false"
                : $"{isPattern.Expression.WithoutTrivia().ToFullString()} is true",
        };
    }

    private static string BuildComparisonFix(
        BinaryExpressionSyntax comparison,
        bool negate,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var innerIsEquals = comparison.IsKind(SyntaxKind.EqualsExpression);

        // Check for HasValue on either side first (id.HasValue is also MemberAccessExpressionSyntax
        // so the generic literal detection would misclassify it)
        var leftHasValue =
            comparison.Left is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } leftAccess
            && semanticModel.GetTypeInfo(leftAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T;
        var rightHasValue =
            comparison.Right is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } rightAccess
            && semanticModel.GetTypeInfo(rightAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T;

        if (leftHasValue || rightHasValue)
        {
            var hasValueAccess = leftHasValue
                ? (MemberAccessExpressionSyntax)comparison.Left
                : (MemberAccessExpressionSyntax)comparison.Right;
            var constSide = leftHasValue ? comparison.Right : comparison.Left;
            var constText = constSide.WithoutTrivia().ToFullString();
            if (constText is "true" or "false")
            {
                var isTrueInner = string.Equals(constText, "true", StringComparison.Ordinal);
                var innerMeansHasValue = innerIsEquals == isTrueInner;
                var finalMeansHasValue = innerMeansHasValue != negate;
                var ownerText = hasValueAccess.Expression.WithoutTrivia().ToFullString();
                return finalMeansHasValue ? $"{ownerText} is not null" : $"{ownerText} is null";
            }
        }

        var leftIsLiteral =
            comparison.Left
            is LiteralExpressionSyntax
                or PrefixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.UnaryMinusExpression,
                    Operand: LiteralExpressionSyntax,
                }
                or MemberAccessExpressionSyntax;
        var variableExpr = leftIsLiteral ? comparison.Right : comparison.Left;
        var constantExpr = leftIsLiteral ? comparison.Left : comparison.Right;

        var resultIsPositive = innerIsEquals != negate;
        var variableText = variableExpr.WithoutTrivia().ToFullString();
        var constantText = constantExpr.WithoutTrivia().ToFullString();

        return resultIsPositive ? $"{variableText} is {constantText}" : $"{variableText} is not {constantText}";
    }

    private static string BuildNegationFix(
        PrefixUnaryExpressionSyntax negation,
        bool isFalse,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var inner = negation.Operand;
        while (inner is ParenthesizedExpressionSyntax paren)
            inner = paren.Expression;

        // !id.HasValue on Nullable<T>
        if (
            inner is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } hasValueAccess
            && semanticModel.GetTypeInfo(hasValueAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T
        )
        {
            var ownerText = hasValueAccess.Expression.WithoutTrivia().ToFullString();
            // !(id.HasValue) is true/not false → id is null
            // !(id.HasValue) is false/not true → id is not null
            return isFalse ? $"{ownerText} is not null" : $"{ownerText} is null";
        }

        // Generic: !(expr) is false → expr is true, !(expr) is true → expr is false
        var operandText = negation.Operand.WithoutTrivia().ToFullString();
        return isFalse ? $"{operandText} is true" : $"{operandText} is false";
    }

    private static string BuildIsPatternFix(
        IsPatternExpressionSyntax isPattern,
        bool negate,
        SemanticModel? semanticModel = null,
        CancellationToken ct = default
    )
    {
        // Resolve inner expression: if it's !(id.HasValue) is true/false, resolve fully
        var innerExpr = isPattern.Expression;
        while (innerExpr is ParenthesizedExpressionSyntax paren)
            innerExpr = paren.Expression;

        if (
            semanticModel is not null
            && innerExpr is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } innerNegation
        )
        {
            var negationInner = innerNegation.Operand;
            while (negationInner is ParenthesizedExpressionSyntax innerParen)
                negationInner = innerParen.Expression;

            if (
                negationInner is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } hasValueAccess
                && semanticModel.GetTypeInfo(hasValueAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                    is SpecialType.System_Nullable_T
            )
            {
                // !(id.HasValue) means "is null". Determine effective meaning with pattern + negate.
                var patternIsFalse = isPattern.Pattern switch
                {
                    ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.FalseLiteralExpression } => true,
                    UnaryPatternSyntax
                    {
                        RawKind: (int)SyntaxKind.NotPattern,
                        Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.TrueLiteralExpression },
                    } => true,
                    _ => false,
                };
                // !(HasValue) = isNull. is true → keep isNull. is false → flip to isNotNull.
                var meansNull = patternIsFalse is false;
                if (negate)
                    meansNull = meansNull is false;

                var ownerText = hasValueAccess.Expression.WithoutTrivia().ToFullString();
                return meansNull ? $"{ownerText} is null" : $"{ownerText} is not null";
            }
        }

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

    private static string FixBinaryExpression(
        BinaryExpressionSyntax binaryExpression,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var left = binaryExpression.Left;
        var right = binaryExpression.Right;

        var leftIsConstant = IsConstant(left, semanticModel, ct);

        var variable = leftIsConstant ? right : left;
        var constant = leftIsConstant ? left : right;

        var isEquals = binaryExpression.IsKind(SyntaxKind.EqualsExpression);

        if (
            TryBuildWrappedComparisonReplacement(
                variable,
                constant,
                isEquals,
                semanticModel,
                ct,
                out var wrappedReplacement
            )
        )
        {
            return wrappedReplacement;
        }

        // id.HasValue == true → id is not null, id.HasValue == false → id is null
        if (TryBuildHasValueReplacement(variable, constant, isEquals, semanticModel, ct, out var hasValueReplacement))
        {
            return hasValueReplacement;
        }

        var variableText = variable.WithoutTrivia().ToFullString();
        var constantText = DefaultValueHelper.IsDefaultExpression(constant)
            ? DefaultValueHelper.ResolveDefaultPatternText(variable, semanticModel, ct)
            : constant.WithoutTrivia().ToFullString();

        return isEquals ? $"{variableText} is {constantText}" : $"{variableText} is not {constantText}";
    }

    private static bool TryBuildHasValueReplacement(
        ExpressionSyntax variable,
        ExpressionSyntax constant,
        bool isEquals,
        SemanticModel semanticModel,
        CancellationToken ct,
        out string replacement
    )
    {
        replacement = "";

        var unwrappedVar = variable;
        while (unwrappedVar is ParenthesizedExpressionSyntax paren)
            unwrappedVar = paren.Expression;

        if (unwrappedVar is not MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } memberAccess)
        {
            return false;
        }

        var ownerType = semanticModel.GetTypeInfo(memberAccess.Expression, ct).Type;
        if (ownerType?.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T)
            return false;

        var constantText = constant.WithoutTrivia().ToFullString();
        if (constantText is not "true" and not "false")
            return false;

        var isTrueConstant = string.Equals(constantText, "true", StringComparison.Ordinal);

        // HasValue == true  → is not null    (has value = not null)
        // HasValue == false → is null        (no value = null)
        // HasValue != true  → is null
        // HasValue != false → is not null
        var meansHasValue = isEquals == isTrueConstant;
        var ownerText = memberAccess.Expression.WithoutTrivia().ToFullString();

        replacement = meansHasValue ? $"{ownerText} is not null" : $"{ownerText} is null";
        return true;
    }

    private static bool TryBuildWrappedComparisonReplacement(
        ExpressionSyntax variable,
        ExpressionSyntax constant,
        bool outerIsEquals,
        SemanticModel semanticModel,
        CancellationToken ct,
        out string replacement
    )
    {
        replacement = "";

        var unwrapped = variable;
        while (unwrapped is ParenthesizedExpressionSyntax paren)
            unwrapped = paren.Expression;

        // Only handle == true, == false, != true, != false
        var constantText = constant.WithoutTrivia().ToFullString();
        if (constantText is not "true" and not "false")
            return false;

        var isTrueConstant = constantText is "true";
        var shouldNegate = outerIsEquals != isTrueConstant;

        // Handle wrapped is-pattern: (o is null) == false → o is not null
        if (unwrapped is IsPatternExpressionSyntax isPattern)
        {
            replacement = BuildIsPatternFix(isPattern, shouldNegate, semanticModel, ct);
            return true;
        }

        // Handle wrapped comparison: (x == c) == false → x is not c
        if (
            unwrapped is not BinaryExpressionSyntax inner
            || inner.Kind() is not SyntaxKind.EqualsExpression and not SyntaxKind.NotEqualsExpression
        )
        {
            return false;
        }

        var innerIsEquals = inner.IsKind(SyntaxKind.EqualsExpression);

        // Check for HasValue first (MemberAccessExpressionSyntax would be misclassified as literal)
        var innerLeftHasValue =
            inner.Left is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } innerLeftAccess
            && semanticModel.GetTypeInfo(innerLeftAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T;
        var innerRightHasValue =
            inner.Right is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } innerRightAccess
            && semanticModel.GetTypeInfo(innerRightAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T;

        if (innerLeftHasValue || innerRightHasValue)
        {
            var hvAccess = innerLeftHasValue
                ? (MemberAccessExpressionSyntax)inner.Left
                : (MemberAccessExpressionSyntax)inner.Right;
            var constSide = innerLeftHasValue ? inner.Right : inner.Left;
            var constVal = constSide.WithoutTrivia().ToFullString();
            if (constVal is "true" or "false")
            {
                var isTrueInner = string.Equals(constVal, "true", StringComparison.Ordinal);
                var innerMeansHasValue = innerIsEquals == isTrueInner;
                var finalMeansHasValue = innerMeansHasValue != shouldNegate;
                var ownerText = hvAccess.Expression.WithoutTrivia().ToFullString();
                replacement = finalMeansHasValue ? $"{ownerText} is not null" : $"{ownerText} is null";
                return true;
            }
        }

        var resultIsPositive = innerIsEquals != shouldNegate;

        // Figure out which side is the inner constant
        var innerLeftIsLiteral =
            inner.Left
            is LiteralExpressionSyntax
                or PrefixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.UnaryMinusExpression,
                    Operand: LiteralExpressionSyntax
                }
                or MemberAccessExpressionSyntax;
        var innerRightIsLiteral =
            inner.Right
            is LiteralExpressionSyntax
                or PrefixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.UnaryMinusExpression,
                    Operand: LiteralExpressionSyntax
                }
                or MemberAccessExpressionSyntax;

        if (innerLeftIsLiteral == innerRightIsLiteral)
            return false;

        var innerVariableExpr = innerLeftIsLiteral ? inner.Right : inner.Left;
        var innerVariable = innerVariableExpr.WithoutTrivia().ToFullString();
        var innerConstant = innerLeftIsLiteral
            ? inner.Left.WithoutTrivia().ToFullString()
            : inner.Right.WithoutTrivia().ToFullString();

        // (id.HasValue == true) == false → id is null
        if (
            innerVariableExpr is MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } hasValueAccess
            && semanticModel.GetTypeInfo(hasValueAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T
            && innerConstant is "true" or "false"
        )
        {
            var isTrueInner = string.Equals(innerConstant, "true", StringComparison.Ordinal);
            var innerMeansHasValue = innerIsEquals == isTrueInner;
            var finalMeansHasValue = innerMeansHasValue != shouldNegate;
            var ownerText = hasValueAccess.Expression.WithoutTrivia().ToFullString();
            replacement = finalMeansHasValue ? $"{ownerText} is not null" : $"{ownerText} is null";
            return true;
        }

        replacement = resultIsPositive
            ? $"{innerVariable} is {innerConstant}"
            : $"{innerVariable} is not {innerConstant}";

        return true;
    }

    private static bool IsConstant(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken ct)
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
                Operand: LiteralExpressionSyntax
            }:
                return true;
            default:
            {
                var constantValue = semanticModel.GetConstantValue(expression, ct);
                return constantValue.HasValue;
            }
        }
    }
}
