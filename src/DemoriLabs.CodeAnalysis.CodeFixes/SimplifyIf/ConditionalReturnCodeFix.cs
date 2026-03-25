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
public sealed class ConditionalReturnCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.SimplifyConditionalReturn];

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
                "Simplify conditional return to ternary",
                ct => FixAsync(context.Document, ifStatement, ct),
                equivalenceKey: nameof(ConditionalReturnCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, IfStatementSyntax ifStatement, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var ifReturnExpr = GetSingleReturnExpression(ifStatement.Statement);
        var condition = ifStatement.Condition.WithoutTrivia().ToFullString();
        var trueValue = ifReturnExpr.WithoutTrivia().ToFullString();

        string falseValue;
        SyntaxNode newRoot;

        if (ifStatement.Else is not null)
        {
            var elseReturnExpr = GetSingleReturnExpression(ifStatement.Else.Statement);
            falseValue = elseReturnExpr.WithoutTrivia().ToFullString();

            var replacementText = $"return {condition} ? {trueValue} : {falseValue};";
            var replacement = SyntaxFactory
                .ParseStatement(replacementText)
                .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

            newRoot = root.ReplaceNode(ifStatement, replacement);
        }
        else
        {
            var parentBlock = (BlockSyntax)ifStatement.Parent!;
            var ifIndex = parentBlock.Statements.IndexOf(ifStatement);
            var nextReturn = parentBlock.Statements[ifIndex + 1];
            var nextReturnExpr = ((ReturnStatementSyntax)nextReturn).Expression!;
            falseValue = nextReturnExpr.WithoutTrivia().ToFullString();

            var replacementText = $"return {condition} ? {trueValue} : {falseValue};";
            var replacement = SyntaxFactory
                .ParseStatement(replacementText)
                .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

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
}
