using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class LogicalPatternCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseLogicalPattern];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not BinaryExpressionSyntax)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use logical pattern",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(LogicalPatternCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null || node is not BinaryExpressionSyntax binaryExpression)
            return document;

        var isOr = binaryExpression.IsKind(SyntaxKind.LogicalOrExpression);
        var leaves = new List<ExpressionSyntax>();
        FlattenChain(binaryExpression, binaryExpression.Kind(), leaves);

        if (leaves.Count < 2)
            return document;

        string replacementText;
        if (isOr)
        {
            replacementText = BuildOrReplacement(leaves);
        }
        else if (AllLeavesAre(leaves, SyntaxKind.NotEqualsExpression))
        {
            replacementText = BuildNotEqualsReplacement(leaves);
        }
        else
        {
            replacementText = BuildRangeReplacement(leaves);
        }

        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(binaryExpression.GetLeadingTrivia())
            .WithTrailingTrivia(binaryExpression.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(binaryExpression, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static string BuildOrReplacement(List<ExpressionSyntax> leaves)
    {
        var variable = GetVariable(leaves[0]).WithoutTrivia().ToFullString();
        var parts = leaves.Select(BuildOrPatternPart);
        return $"{variable} is {string.Join(" or ", parts)}";
    }

    private static string BuildOrPatternPart(ExpressionSyntax leaf)
    {
        // !x.HasValue → null
        if (leaf is PrefixUnaryExpressionSyntax)
            return "null";

        var comparison = (BinaryExpressionSyntax)leaf;

        // x == constant → constant
        if (comparison.IsKind(SyntaxKind.EqualsExpression))
            return GetConstant(comparison);

        // x > constant → > constant (relational)
        return BuildRelationalPattern(comparison);
    }

    private static string BuildNotEqualsReplacement(List<ExpressionSyntax> leaves)
    {
        var variable = GetVariable(leaves[0]).WithoutTrivia().ToFullString();
        var parts = new List<string>();
        foreach (var leaf in leaves)
        {
            // x.HasValue → not null
            if (leaf is MemberAccessExpressionSyntax)
            {
                parts.Add("not null");
            }
            else
            {
                parts.Add($"not {GetConstant((BinaryExpressionSyntax)leaf)}");
            }
        }

        return $"{variable} is {string.Join(" and ", parts)}";
    }

    private static string BuildRangeReplacement(List<ExpressionSyntax> leaves)
    {
        var variable = GetVariable(leaves[0]).WithoutTrivia().ToFullString();
        var part0 = BuildRelationalPattern((BinaryExpressionSyntax)leaves[0]);
        var part1 = BuildRelationalPattern((BinaryExpressionSyntax)leaves[1]);
        return $"{variable} is {part0} and {part1}";
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

    private static void FlattenChain(BinaryExpressionSyntax expression, SyntaxKind kind, List<ExpressionSyntax> leaves)
    {
        switch (expression.Left)
        {
            case BinaryExpressionSyntax leftBinary when leftBinary.Kind() == kind:
                FlattenChain(leftBinary, kind, leaves);
                break;
            case BinaryExpressionSyntax leftLeaf:
                leaves.Add(leftLeaf);
                break;
            case PrefixUnaryExpressionSyntax leftNot when IsNegatedHasValue(leftNot):
                leaves.Add(leftNot);
                break;
            case MemberAccessExpressionSyntax leftHasValue when IsHasValueAccess(leftHasValue):
                leaves.Add(leftHasValue);
                break;
        }

        switch (expression.Right)
        {
            case BinaryExpressionSyntax rightLeaf:
                leaves.Add(rightLeaf);
                break;
            case PrefixUnaryExpressionSyntax rightNot when IsNegatedHasValue(rightNot):
                leaves.Add(rightNot);
                break;
            case MemberAccessExpressionSyntax rightHasValue when IsHasValueAccess(rightHasValue):
                leaves.Add(rightHasValue);
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

    private static bool AllLeavesAre(List<ExpressionSyntax> leaves, SyntaxKind kind)
    {
        foreach (var leaf in leaves)
        {
            // x.HasValue counts as != null for && chains
            if (leaf is MemberAccessExpressionSyntax)
                continue;

            if (leaf is not BinaryExpressionSyntax binary || binary.Kind() != kind)
                return false;
        }

        return true;
    }

    private static ExpressionSyntax GetVariable(ExpressionSyntax leaf)
    {
        // !x.HasValue → x
        if (leaf is PrefixUnaryExpressionSyntax { Operand: MemberAccessExpressionSyntax negatedAccess })
            return negatedAccess.Expression;

        // x.HasValue → x
        if (leaf is MemberAccessExpressionSyntax hasValueAccess)
            return hasValueAccess.Expression;

        var comparison = (BinaryExpressionSyntax)leaf;
        return IsLiteralOrConstant(comparison.Right) ? comparison.Left : comparison.Right;
    }

    private static string GetConstant(BinaryExpressionSyntax comparison)
    {
        var constant = IsLiteralOrConstant(comparison.Right) ? comparison.Right : comparison.Left;
        return constant.WithoutTrivia().ToFullString();
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
}
