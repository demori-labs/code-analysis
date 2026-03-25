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

    private static void AnalyzeLogicalExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        if (binaryExpression.Parent is BinaryExpressionSyntax parent && parent.Kind() == binaryExpression.Kind())
            return;

        var isOr = binaryExpression.IsKind(SyntaxKind.LogicalOrExpression);

        var leaves = new List<BinaryExpressionSyntax>();
        FlattenChain(binaryExpression, binaryExpression.Kind(), leaves);

        if (leaves.Count < 2)
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
        List<BinaryExpressionSyntax> leaves,
        INamedTypeSymbol? expressionType
    )
    {
        if (AllLeavesAreEquality(leaves, SyntaxKind.EqualsExpression) is false)
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
        var constants = string.Join(" or ", GetConstants(leaves));
        var suggestion = $"{variableText} is {constants}";

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, outerExpression.GetLocation(), suggestion, outerExpression.ToString())
        );
    }

    private static void TryReportAndPattern(
        SyntaxNodeAnalysisContext context,
        BinaryExpressionSyntax outerExpression,
        List<BinaryExpressionSyntax> leaves,
        INamedTypeSymbol? expressionType
    )
    {
        if (AllLeavesAreEquality(leaves, SyntaxKind.NotEqualsExpression))
        {
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
            var parts = new List<string>();
            foreach (var leaf in leaves)
            {
                parts.Add($"not {GetConstant(leaf)}");
            }

            var suggestion = $"{variableText} is {string.Join(" and ", parts)}";

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, outerExpression.GetLocation(), suggestion, outerExpression.ToString())
            );
            return;
        }

        if (leaves.Count is not 2)
            return;

        if (AllLeavesAreRelational(leaves) is false)
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

        var rangeVariable = GetVariable(leaves[0]).ToString();
        var part0 = BuildRelationalPattern(leaves[0]);
        var part1 = BuildRelationalPattern(leaves[1]);
        var rangeSuggestion = $"{rangeVariable} is {part0} and {part1}";

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, outerExpression.GetLocation(), rangeSuggestion, outerExpression.ToString())
        );
    }

    private static void FlattenChain(
        BinaryExpressionSyntax expression,
        SyntaxKind kind,
        List<BinaryExpressionSyntax> leaves
    )
    {
        switch (expression.Left)
        {
            case BinaryExpressionSyntax leftBinary when leftBinary.Kind() == kind:
                FlattenChain(leftBinary, kind, leaves);
                break;
            case BinaryExpressionSyntax leftLeaf when IsComparisonExpression(leftLeaf):
                leaves.Add(leftLeaf);
                break;
            default:
                leaves.Clear();
                return;
        }

        if (expression.Right is BinaryExpressionSyntax rightLeaf && IsComparisonExpression(rightLeaf))
        {
            leaves.Add(rightLeaf);
        }
        else
        {
            leaves.Clear();
        }
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

    private static bool AllLeavesAreEquality(List<BinaryExpressionSyntax> leaves, SyntaxKind kind)
    {
        foreach (var leaf in leaves)
        {
            if (leaf.Kind() != kind)
                return false;

            if (HasConstantOperand(leaf) is false)
                return false;

            if (HasStringLiteralOperand(leaf))
                return false;
        }

        return true;
    }

    private static bool HasStringLiteralOperand(BinaryExpressionSyntax expr)
    {
        return expr.Left is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
            || expr.Right is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression };
    }

    private static bool AllLeavesAreRelational(List<BinaryExpressionSyntax> leaves)
    {
        foreach (var leaf in leaves)
        {
            if (
                leaf.Kind()
                is not SyntaxKind.LessThanExpression
                    and not SyntaxKind.LessThanOrEqualExpression
                    and not SyntaxKind.GreaterThanExpression
                    and not SyntaxKind.GreaterThanOrEqualExpression
            )
            {
                return false;
            }

            if (HasConstantOperand(leaf) is false)
                return false;
        }

        return true;
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
        List<BinaryExpressionSyntax> leaves,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        ISymbol? firstSymbol = null;
        foreach (var leaf in leaves)
        {
            var variable = GetVariable(leaf);
            var symbol = semanticModel.GetSymbolInfo(variable, ct).Symbol;
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

    private static ExpressionSyntax GetVariable(BinaryExpressionSyntax comparison)
    {
        return IsLiteralOrConstant(comparison.Right) ? comparison.Left : comparison.Right;
    }

    private static string GetConstant(BinaryExpressionSyntax comparison)
    {
        var constant = IsLiteralOrConstant(comparison.Right) ? comparison.Right : comparison.Left;
        return constant.WithoutTrivia().ToString();
    }

    private static IEnumerable<string> GetConstants(List<BinaryExpressionSyntax> leaves)
    {
        return leaves.Select(GetConstant);
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
