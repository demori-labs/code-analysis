using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.StringEquals;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class UseStringEqualsCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseStringEqualsWithComparison];

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
                "Use string.Equals with StringComparison",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(UseStringEqualsCodeFix)
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

        var replacementText = node switch
        {
            BinaryExpressionSyntax binary => BuildBinaryReplacement(binary),
            IsPatternExpressionSyntax isPattern => BuildIsPatternReplacement(isPattern),
            _ => null,
        };

        if (replacementText is null)
            return document;

        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(node, replacement);
        newRoot = newRoot.EnsureUsingDirective(semanticModel, "System");

        return document.WithSyntaxRoot(newRoot);
    }

    private static string BuildBinaryReplacement(BinaryExpressionSyntax binary)
    {
        var leftText = binary.Left.WithoutTrivia().ToFullString();
        var rightText = binary.Right.WithoutTrivia().ToFullString();

        return binary.IsKind(SyntaxKind.EqualsExpression)
            ? $"string.Equals({leftText}, {rightText}, StringComparison.Ordinal)"
            : $"string.Equals({leftText}, {rightText}, StringComparison.Ordinal) is false";
    }

    private static string? BuildIsPatternReplacement(IsPatternExpressionSyntax isPattern)
    {
        var exprText = isPattern.Expression.WithoutTrivia().ToFullString();

        return isPattern.Pattern switch
        {
            ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.StringLiteralExpression } constant =>
                $"string.Equals({exprText}, {constant.Expression}, StringComparison.Ordinal)",
            UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax
                {
                    Expression.RawKind: (int)SyntaxKind.StringLiteralExpression,
                } innerConstant,
            } => $"string.Equals({exprText}, {innerConstant.Expression}, StringComparison.Ordinal) is false",
            _ => null,
        };
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
                    Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.StringLiteralExpression }
                        or UnaryPatternSyntax
                        {
                            RawKind: (int)SyntaxKind.NotPattern,
                            Pattern: ConstantPatternSyntax
                            {
                                Expression.RawKind: (int)SyntaxKind.StringLiteralExpression,
                            },
                        },
                };
    }
}
