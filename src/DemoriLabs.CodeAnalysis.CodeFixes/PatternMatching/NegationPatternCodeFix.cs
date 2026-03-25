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
public sealed class NegationPatternCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseNegationPattern];

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

        if (node is not PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression })
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use pattern matching",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(NegationPatternCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        if (node is not PrefixUnaryExpressionSyntax prefixUnary)
            return document;

        var operand = prefixUnary.Operand;

        var unwrapped = operand;
        while (unwrapped is ParenthesizedExpressionSyntax paren)
            unwrapped = paren.Expression;

        string replacementText;

        switch (unwrapped)
        {
            // !(x is SomePattern) → x is not SomePattern
            case IsPatternExpressionSyntax isPattern:
            {
                var expr = isPattern.Expression.WithoutTrivia().ToFullString();
                var pattern = isPattern.Pattern.WithoutTrivia().ToFullString();
                replacementText = $"{expr} is not {pattern}";
                break;
            }

            // !(x is T) — old-style IsExpression → x is not T
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpr:
            {
                var expr = isExpr.Left.WithoutTrivia().ToFullString();
                var type = isExpr.Right.WithoutTrivia().ToFullString();
                replacementText = $"{expr} is not {type}";
                break;
            }

            // !flag → flag is false
            default:
            {
                var operandText = operand.WithoutTrivia().ToFullString();
                replacementText = $"{operandText} is false";
                break;
            }
        }

        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(prefixUnary.GetLeadingTrivia())
            .WithTrailingTrivia(prefixUnary.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(prefixUnary, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
