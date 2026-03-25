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
        if (root is null)
            return document;

        if (node is not BinaryExpressionSyntax binaryExpression)
            return document;

        var isOr = binaryExpression.IsKind(SyntaxKind.LogicalOrExpression);
        var leaves = new List<BinaryExpressionSyntax>();
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

    private static string BuildOrReplacement(List<BinaryExpressionSyntax> leaves)
    {
        var variable = GetVariable(leaves[0]).WithoutTrivia().ToFullString();
        var parts = new List<string>();
        foreach (var leaf in leaves)
        {
            parts.Add(GetConstant(leaf));
        }

        return $"{variable} is {string.Join(" or ", parts)}";
    }

    private static string BuildNotEqualsReplacement(List<BinaryExpressionSyntax> leaves)
    {
        var variable = GetVariable(leaves[0]).WithoutTrivia().ToFullString();
        var parts = new List<string>();
        foreach (var leaf in leaves)
        {
            parts.Add($"not {GetConstant(leaf)}");
        }

        return $"{variable} is {string.Join(" and ", parts)}";
    }

    private static string BuildRangeReplacement(List<BinaryExpressionSyntax> leaves)
    {
        var variable = GetVariable(leaves[0]).WithoutTrivia().ToFullString();
        var part0 = BuildRelationalPattern(leaves[0]);
        var part1 = BuildRelationalPattern(leaves[1]);
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

    private static void FlattenChain(
        BinaryExpressionSyntax expression,
        SyntaxKind kind,
        List<BinaryExpressionSyntax> leaves
    )
    {
        if (expression.Left is BinaryExpressionSyntax leftBinary && leftBinary.Kind() == kind)
            FlattenChain(leftBinary, kind, leaves);
        else if (expression.Left is BinaryExpressionSyntax leftLeaf)
            leaves.Add(leftLeaf);

        if (expression.Right is BinaryExpressionSyntax rightLeaf)
            leaves.Add(rightLeaf);
    }

    private static bool AllLeavesAre(List<BinaryExpressionSyntax> leaves, SyntaxKind kind)
    {
        return leaves.All(leaf => leaf.Kind() == kind);
    }

    private static ExpressionSyntax GetVariable(BinaryExpressionSyntax comparison)
    {
        return IsLiteralOrConstant(comparison.Right) ? comparison.Left : comparison.Right;
    }

    private static string GetConstant(BinaryExpressionSyntax comparison)
    {
        var constant = IsLiteralOrConstant(comparison.Right) ? comparison.Right : comparison.Left;
        return constant.WithoutTrivia().ToFullString();
    }

    private static bool IsLiteralOrConstant(ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax)
            return true;

        if (
            expr is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } prefix
            && prefix.Operand is LiteralExpressionSyntax
        )
        {
            return true;
        }

        if (expr is MemberAccessExpressionSyntax)
            return true;

        return false;
    }
}
