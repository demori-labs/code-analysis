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
public sealed class BooleanReturnCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.SimplifyBooleanReturn];

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
                "Simplify boolean return",
                ct => FixAsync(context.Document, ifStatement, ct),
                equivalenceKey: nameof(BooleanReturnCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, IfStatementSyntax ifStatement, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var ifReturnValue = GetBooleanLiteralValue(GetSingleReturnExpression(ifStatement.Statement));
        var condition = ifStatement.Condition.WithoutTrivia().ToFullString();
        var replacementText = ifReturnValue ? $"return {condition};" : $"return {condition} is false;";

        var replacement = SyntaxFactory
            .ParseStatement(replacementText)
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

        SyntaxNode newRoot;

        if (ifStatement.Else is not null)
        {
            newRoot = root.ReplaceNode(ifStatement, replacement);
        }
        else
        {
            var parentBlock = (BlockSyntax)ifStatement.Parent!;
            var ifIndex = parentBlock.Statements.IndexOf(ifStatement);
            var nextReturn = parentBlock.Statements[ifIndex + 1];

            var nodesToRemove = new SyntaxNode[] { ifStatement, nextReturn };
            newRoot = root.TrackNodes(nodesToRemove);
            newRoot = newRoot.ReplaceNode(newRoot.GetCurrentNode(ifStatement)!, replacement);
            newRoot = newRoot.RemoveNode(newRoot.GetCurrentNode(nextReturn)!, SyntaxRemoveOptions.KeepNoTrivia)!;
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionSyntax GetSingleReturnExpression(StatementSyntax statement)
    {
        return statement switch
        {
            ReturnStatementSyntax { Expression: { } expr } => expr,
            BlockSyntax { Statements.Count: 1 } block
                when block.Statements[0] is ReturnStatementSyntax { Expression: { } expr } => expr,
            _ => throw new InvalidOperationException("Expected a single return statement."),
        };
    }

    private static bool GetBooleanLiteralValue(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression };
    }
}
