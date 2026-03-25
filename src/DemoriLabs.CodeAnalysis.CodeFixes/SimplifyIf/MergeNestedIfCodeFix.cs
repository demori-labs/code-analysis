using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class MergeNestedIfCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.MergeNestedIf];

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
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);

        if (token.Parent is not IfStatementSyntax ifStatement)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Merge nested if statements",
                ct => FixAsync(context.Document, ifStatement, ct),
                equivalenceKey: nameof(MergeNestedIfCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, IfStatementSyntax ifStatement, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var block = (BlockSyntax)ifStatement.Statement;
        var innerIf = (IfStatementSyntax)block.Statements[0];

        var outerCondition = MaybeParenthesize(ifStatement.Condition);
        var innerCondition = MaybeParenthesize(innerIf.Condition);

        var innerBody = ReindentBody(innerIf.Statement, ifStatement, innerIf);

        var combinedCondition = SyntaxFactory.ParseExpression($"{outerCondition} && {innerCondition}");

        var replacement = ifStatement
            .WithCondition(combinedCondition)
            .WithStatement(innerBody)
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(ifStatement, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static StatementSyntax ReindentBody(
        StatementSyntax innerBody,
        IfStatementSyntax outerIf,
        IfStatementSyntax innerIf
    )
    {
        var outerIndent = GetIndentation(outerIf);
        var innerIndent = GetIndentation(innerIf);

        if (outerIndent == innerIndent)
            return innerBody;

        return innerBody.ReplaceTokens(
            innerBody.DescendantTokens(),
            (original, _) =>
            {
                var leadingTrivia = original.LeadingTrivia;
                var hasChange = false;
                var newTrivia = new SyntaxTriviaList();

                foreach (var trivia in leadingTrivia)
                {
                    if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) && trivia.ToString().StartsWith(innerIndent))
                    {
                        var replaced = outerIndent + trivia.ToString().Substring(innerIndent.Length);
                        newTrivia = newTrivia.Add(SyntaxFactory.Whitespace(replaced));
                        hasChange = true;
                    }
                    else
                    {
                        newTrivia = newTrivia.Add(trivia);
                    }
                }

                return hasChange ? original.WithLeadingTrivia(newTrivia) : original;
            }
        );
    }

    private static string GetIndentation(SyntaxNode node)
    {
        return node.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
                .Select(t => t.ToString())
                .FirstOrDefault()
            ?? string.Empty;
    }

    private static string MaybeParenthesize(ExpressionSyntax expression)
    {
        var text = expression.WithoutTrivia().ToFullString();

        return NeedsParentheses(expression) ? $"({text})" : text;
    }

    private static bool NeedsParentheses(ExpressionSyntax expression)
    {
        return expression is BinaryExpressionSyntax binary
            && binary.Kind() is SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression;
    }
}
