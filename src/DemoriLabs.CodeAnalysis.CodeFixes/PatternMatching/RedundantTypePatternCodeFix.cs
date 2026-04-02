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
public sealed class RedundantTypePatternCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.RedundantTypePattern];

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

        if (node is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpression)
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (semanticModel is null)
            return;

        var exprTypeInfo = semanticModel.GetTypeInfo(isExpression.Left, context.CancellationToken);
        if (exprTypeInfo.Nullability.FlowState is not NullableFlowState.MaybeNull)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use 'is not null'",
                ct => FixAsync(context.Document, isExpression, ct),
                equivalenceKey: nameof(RedundantTypePatternCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(
        Document document,
        BinaryExpressionSyntax isExpression,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var replacement = SyntaxFactory
            .IsPatternExpression(
                isExpression.Left,
                SyntaxFactory.UnaryPattern(
                    SyntaxFactory.Token(SyntaxKind.NotKeyword).WithLeadingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.ConstantPattern(
                        SyntaxFactory
                            .LiteralExpression(SyntaxKind.NullLiteralExpression)
                            .WithLeadingTrivia(SyntaxFactory.Space)
                    )
                )
            )
            .WithLeadingTrivia(isExpression.GetLeadingTrivia())
            .WithTrailingTrivia(isExpression.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(isExpression, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
