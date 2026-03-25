using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.NullSimplification;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class NullConditionalAssignmentCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseNullConditionalAssignment];

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
                "Use null-conditional assignment",
                ct => FixAsync(context.Document, ifStatement, ct),
                equivalenceKey: nameof(NullConditionalAssignmentCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, IfStatementSyntax ifStatement, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var bodyStatement = GetSingleStatement(ifStatement.Statement);
        var bodyText = bodyStatement.WithoutTrivia().ToFullString();
        var variableName = GetCheckedVariableName(ifStatement.Condition);

        var replacementText = bodyText.Replace(variableName + ".", variableName + "?.");
        var replacement = SyntaxFactory
            .ParseStatement(replacementText)
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(ifStatement, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionStatementSyntax GetSingleStatement(StatementSyntax statement)
    {
        return statement switch
        {
            ExpressionStatementSyntax expressionStatement => expressionStatement,
            BlockSyntax { Statements.Count: 1 } block
                when block.Statements[0] is ExpressionStatementSyntax expressionStatement => expressionStatement,
            _ => throw new InvalidOperationException("Expected a single expression statement."),
        };
    }

    private static string GetCheckedVariableName(ExpressionSyntax condition)
    {
        return condition switch
        {
            BinaryExpressionSyntax { Left: IdentifierNameSyntax leftId } => leftId.Identifier.Text,
            BinaryExpressionSyntax { Right: IdentifierNameSyntax rightId } => rightId.Identifier.Text,
            IsPatternExpressionSyntax { Expression: IdentifierNameSyntax id } => id.Identifier.Text,
            _ => throw new InvalidOperationException("Expected a not-null check condition."),
        };
    }
}
