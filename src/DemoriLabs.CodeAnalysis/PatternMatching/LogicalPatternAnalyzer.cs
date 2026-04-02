using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LogicalPatternAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseLogicalPattern,
        title: "Use logical pattern",
        messageFormat: "Use '{0}' instead of '{1}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer logical patterns ('is X or Y', 'is >= A and < B') over chained comparison operators on the same variable."
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
                analysisContext => AnalyzeLogicalExpression(analysisContext, expressionType),
                SyntaxKind.LogicalOrExpression,
                SyntaxKind.LogicalAndExpression
            );
        });
    }

    /// <summary>
    /// Checks whether the given expression is a leaf inside a logical pattern chain
    /// that this analyzer would report. Used by other analyzers to suppress overlapping diagnostics.
    /// </summary>
    internal static bool IsLeafOfLogicalPatternChain(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        if (
            expression.Parent is not BinaryExpressionSyntax parent
            || parent.Kind() is not SyntaxKind.LogicalOrExpression and not SyntaxKind.LogicalAndExpression
        )
        {
            return false;
        }

        var kind = parent.Kind();
        while (parent.Parent is BinaryExpressionSyntax grandParent && grandParent.Kind() == kind)
            parent = grandParent;

        var leaves = new List<ExpressionSyntax>();
        FlattenChain(parent, kind, leaves);

        if (leaves.Count < 2 || ContainsNonNullableHasValue(leaves, semanticModel, ct))
            return false;

        var isOr = kind is SyntaxKind.LogicalOrExpression;

        if (isOr)
        {
            return AllLeavesAreOrCompatible(leaves) && AllLeavesCompareSameSymbol(leaves, semanticModel, ct);
        }

        if (AllLeavesAreAndCompatible(leaves))
            return AllLeavesCompareSameSymbol(leaves, semanticModel, ct);

        return false;
    }

    private static void AnalyzeLogicalExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        if (binaryExpression.Parent is BinaryExpressionSyntax parent && parent.Kind() == binaryExpression.Kind())
            return;

        var isOr = binaryExpression.IsKind(SyntaxKind.LogicalOrExpression);

        var leaves = new List<ExpressionSyntax>();
        FlattenChain(binaryExpression, binaryExpression.Kind(), leaves);

        if (leaves.Count < 2 || ContainsNonNullableHasValue(leaves, context.SemanticModel, context.CancellationToken))
            return;

        if (isOr)
        {
            TryReportOrPattern(context, binaryExpression, leaves, expressionType);
        }
        else
        {
            TryReportAndPattern(context, binaryExpression, leaves, expressionType);
        }
    }

    private static void TryReportOrPattern(
        SyntaxNodeAnalysisContext context,
        BinaryExpressionSyntax outerExpression,
        List<ExpressionSyntax> leaves,
        INamedTypeSymbol? expressionType
    )
    {
        if (AllLeavesAreOrCompatible(leaves) is false)
            return;

        if (AllLeavesCompareSameSymbol(leaves, context.SemanticModel, context.CancellationToken) is false)
            return;

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                outerExpression,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var variableText = GetVariable(leaves[0]).ToString();
        var patterns = string.Join(" or ", leaves.Select(BuildOrPatternPart));
        var suggestion = $"{variableText} is {patterns}";

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, outerExpression.GetLocation(), suggestion, outerExpression.ToString())
        );
    }

    private static void TryReportAndPattern(
        SyntaxNodeAnalysisContext context,
        BinaryExpressionSyntax outerExpression,
        List<ExpressionSyntax> leaves,
        INamedTypeSymbol? expressionType
    )
    {
        if (AllLeavesAreAndCompatible(leaves) is false)
            return;

        if (AllLeavesCompareSameSymbol(leaves, context.SemanticModel, context.CancellationToken) is false)
            return;

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                outerExpression,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var variableText = GetVariable(leaves[0]).ToString();
        var parts = string.Join(" and ", leaves.Select(BuildAndPatternPart));
        var suggestion = $"{variableText} is {parts}";

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, outerExpression.GetLocation(), suggestion, outerExpression.ToString())
        );
    }

    private static void FlattenChain(BinaryExpressionSyntax expression, SyntaxKind kind, List<ExpressionSyntax> leaves)
    {
        switch (expression.Left)
        {
            case BinaryExpressionSyntax leftBinary when leftBinary.Kind() == kind:
                FlattenChain(leftBinary, kind, leaves);
                break;
            case BinaryExpressionSyntax leftLeaf when IsComparisonExpression(leftLeaf):
                leaves.Add(leftLeaf);
                break;
            case PrefixUnaryExpressionSyntax leftNot when IsNegatedHasValue(leftNot):
                leaves.Add(leftNot);
                break;
            case MemberAccessExpressionSyntax leftHasValue when IsHasValueAccess(leftHasValue):
                leaves.Add(leftHasValue);
                break;
            case IsPatternExpressionSyntax leftIsPattern:
                leaves.Add(leftIsPattern);
                break;
            default:
                leaves.Clear();
                return;
        }

        switch (expression.Right)
        {
            case BinaryExpressionSyntax rightLeaf when IsComparisonExpression(rightLeaf):
                leaves.Add(rightLeaf);
                break;
            case PrefixUnaryExpressionSyntax rightNot when IsNegatedHasValue(rightNot):
                leaves.Add(rightNot);
                break;
            case MemberAccessExpressionSyntax rightHasValue when IsHasValueAccess(rightHasValue):
                leaves.Add(rightHasValue);
                break;
            case IsPatternExpressionSyntax rightIsPattern:
                leaves.Add(rightIsPattern);
                break;
            default:
                leaves.Clear();
                break;
        }
    }

    private static bool IsNegatedHasValue(PrefixUnaryExpressionSyntax expr)
    {
        return expr.IsKind(SyntaxKind.LogicalNotExpression)
            && expr.Operand is MemberAccessExpressionSyntax memberAccess
            && IsHasValueAccess(memberAccess);
    }

    private static bool IsHasValueAccess(MemberAccessExpressionSyntax expr)
    {
        return string.Equals(expr.Name.Identifier.Text, "HasValue", StringComparison.Ordinal);
    }

    private static bool ContainsNonNullableHasValue(
        List<ExpressionSyntax> leaves,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        foreach (var leaf in leaves)
        {
            if (IsHasValueLeaf(leaf) is false)
                continue;

            var variable = GetVariable(leaf);
            var type = semanticModel.GetTypeInfo(variable, ct).Type;
            if (type?.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T)
                return true;
        }

        return false;
    }

    private static bool IsHasValueLeaf(ExpressionSyntax leaf)
    {
        return leaf switch
        {
            PrefixUnaryExpressionSyntax prefix => IsNegatedHasValue(prefix),
            MemberAccessExpressionSyntax access => IsHasValueAccess(access),
            _ => false,
        };
    }

    private static bool IsComparisonExpression(BinaryExpressionSyntax expr)
    {
        return expr.Kind()
            is SyntaxKind.EqualsExpression
                or SyntaxKind.NotEqualsExpression
                or SyntaxKind.LessThanExpression
                or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanExpression
                or SyntaxKind.GreaterThanOrEqualExpression;
    }

    private static bool AllLeavesAreAndCompatible(List<ExpressionSyntax> leaves)
    {
        foreach (var leaf in leaves)
        {
            switch (leaf)
            {
                case PrefixUnaryExpressionSyntax prefixUnary when IsNegatedHasValue(prefixUnary):
                case MemberAccessExpressionSyntax memberAccess when IsHasValueAccess(memberAccess):
                case IsPatternExpressionSyntax:
                    continue;
                case BinaryExpressionSyntax binary:
                {
                    if (
                        binary.Kind()
                        is not SyntaxKind.NotEqualsExpression
                            and not SyntaxKind.LessThanExpression
                            and not SyntaxKind.LessThanOrEqualExpression
                            and not SyntaxKind.GreaterThanExpression
                            and not SyntaxKind.GreaterThanOrEqualExpression
                    )
                    {
                        return false;
                    }

                    if (HasConstantOperand(binary) is false)
                        return false;

                    if (HasStringLiteralOperand(binary))
                        return false;

                    continue;
                }
                default:
                    return false;
            }
        }

        return true;
    }

    private static string BuildAndPatternPart(ExpressionSyntax leaf)
    {
        // x.HasValue → not null
        if (leaf is MemberAccessExpressionSyntax)
            return "not null";

        // !x.HasValue → null (shouldn't appear in && but handle gracefully)
        if (leaf is PrefixUnaryExpressionSyntax)
            return "null";

        // x is not null → not null, x is > 0 → > 0
        if (leaf is IsPatternExpressionSyntax isPattern)
            return isPattern.Pattern.WithoutTrivia().ToString();

        var comparison = (BinaryExpressionSyntax)leaf;

        // x != constant → not constant
        if (comparison.IsKind(SyntaxKind.NotEqualsExpression))
            return $"not {GetConstant(comparison)}";

        // x > constant → > constant (relational)
        return BuildRelationalPattern(comparison);
    }

    private static bool AllLeavesAreOrCompatible(List<ExpressionSyntax> leaves)
    {
        foreach (var leaf in leaves)
        {
            if (leaf is PrefixUnaryExpressionSyntax prefixUnary && IsNegatedHasValue(prefixUnary))
                continue;

            if (leaf is IsPatternExpressionSyntax)
                continue;

            if (leaf is not BinaryExpressionSyntax binary)
                return false;

            if (
                binary.Kind()
                is not SyntaxKind.EqualsExpression
                    and not SyntaxKind.LessThanExpression
                    and not SyntaxKind.LessThanOrEqualExpression
                    and not SyntaxKind.GreaterThanExpression
                    and not SyntaxKind.GreaterThanOrEqualExpression
            )
            {
                return false;
            }

            if (HasConstantOperand(binary) is false)
                return false;

            if (HasStringLiteralOperand(binary))
                return false;
        }

        return true;
    }

    private static string BuildOrPatternPart(ExpressionSyntax leaf)
    {
        return leaf switch
        {
            PrefixUnaryExpressionSyntax => "null",
            IsPatternExpressionSyntax isPattern => isPattern.Pattern.WithoutTrivia().ToString(),
            BinaryExpressionSyntax comparison when comparison.IsKind(SyntaxKind.EqualsExpression) => GetConstant(
                comparison
            ),
            BinaryExpressionSyntax comparison => BuildRelationalPattern(comparison),
            _ => throw new InvalidOperationException($"Unexpected leaf type: {leaf.GetType().Name}"),
        };
    }

    private static bool HasStringLiteralOperand(BinaryExpressionSyntax expr)
    {
        return expr.Left is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
            || expr.Right is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression };
    }

    private static bool HasConstantOperand(BinaryExpressionSyntax expr)
    {
        return IsLiteralOrConstant(expr.Left) || IsLiteralOrConstant(expr.Right);
    }

    private static bool IsLiteralOrConstant(ExpressionSyntax expr)
    {
        return expr switch
        {
            LiteralExpressionSyntax
            or PrefixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.UnaryMinusExpression,
                Operand: LiteralExpressionSyntax,
            }
            or MemberAccessExpressionSyntax => true,
            _ => false,
        };
    }

    private static bool AllLeavesCompareSameSymbol(
        List<ExpressionSyntax> leaves,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        ISymbol? firstSymbol = null;
        foreach (var leaf in leaves)
        {
            var leafVariable = GetVariable(leaf);
            var symbol = ResolveNullableSymbol(leafVariable, semanticModel, ct);
            if (symbol is null)
                return false;

            if (firstSymbol is null)
            {
                firstSymbol = symbol;
            }
            else if (SymbolEqualityComparer.Default.Equals(firstSymbol, symbol) is false)
            {
                return false;
            }
        }

        return firstSymbol is not null;
    }

    /// <summary>
    /// Resolves the symbol for a variable expression, unwrapping .Value on Nullable&lt;T&gt;
    /// so that <c>id</c> and <c>id.Value</c> resolve to the same symbol.
    /// </summary>
    private static ISymbol? ResolveNullableSymbol(
        ExpressionSyntax variable,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        // id.Value → unwrap to id if the receiver is Nullable<T>
        if (
            variable is MemberAccessExpressionSyntax { Name.Identifier.Text: "Value" } memberAccess
            && semanticModel.GetTypeInfo(memberAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                is SpecialType.System_Nullable_T
        )
        {
            return semanticModel.GetSymbolInfo(memberAccess.Expression, ct).Symbol;
        }

        return semanticModel.GetSymbolInfo(variable, ct).Symbol;
    }

    private static ExpressionSyntax GetVariable(ExpressionSyntax leaf)
    {
        return leaf switch
        {
            PrefixUnaryExpressionSyntax { Operand: MemberAccessExpressionSyntax negatedAccess } =>
                negatedAccess.Expression,
            MemberAccessExpressionSyntax hasValueAccess when IsHasValueAccess(hasValueAccess) =>
                hasValueAccess.Expression,
            IsPatternExpressionSyntax isPattern => isPattern.Expression,
            BinaryExpressionSyntax comparison => IsLiteralOrConstant(comparison.Right)
                ? comparison.Left
                : comparison.Right,
            _ => throw new InvalidOperationException($"Unexpected leaf type: {leaf.GetType().Name}"),
        };
    }

    private static string GetConstant(ExpressionSyntax leaf)
    {
        // !x.HasValue → null, x.HasValue → null
        if (leaf is PrefixUnaryExpressionSyntax or MemberAccessExpressionSyntax)
            return "null";

        var comparison = (BinaryExpressionSyntax)leaf;
        var constant = IsLiteralOrConstant(comparison.Right) ? comparison.Right : comparison.Left;
        return constant.WithoutTrivia().ToString();
    }

    private static string BuildRelationalPattern(BinaryExpressionSyntax comparison)
    {
        var constant = GetConstant(comparison);
        var variableOnLeft = IsLiteralOrConstant(comparison.Right);

        return comparison.Kind() switch
        {
            SyntaxKind.LessThanExpression => variableOnLeft ? $"< {constant}" : $"> {constant}",
            SyntaxKind.LessThanOrEqualExpression => variableOnLeft ? $"<= {constant}" : $">= {constant}",
            SyntaxKind.GreaterThanExpression => variableOnLeft ? $"> {constant}" : $"< {constant}",
            SyntaxKind.GreaterThanOrEqualExpression => variableOnLeft ? $">= {constant}" : $"<= {constant}",
            _ => constant,
        };
    }
}
