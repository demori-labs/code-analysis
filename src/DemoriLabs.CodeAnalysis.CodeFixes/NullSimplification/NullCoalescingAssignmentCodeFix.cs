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
public sealed class NullCoalescingAssignmentCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseNullCoalescingAssignment];

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
                "Use null-coalescing assignment",
                ct => FixAsync(context.Document, ifStatement, ct),
                equivalenceKey: nameof(NullCoalescingAssignmentCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, IfStatementSyntax ifStatement, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var assignment = GetSingleAssignment(ifStatement.Statement);
        var variableText = assignment.Left.WithoutTrivia().ToFullString();
        var valueText = assignment.Right.WithoutTrivia().ToFullString();

        var replacementText = $"{variableText} ??= {valueText};";
        var replacement = SyntaxFactory
            .ParseStatement(replacementText)
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(ifStatement, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static AssignmentExpressionSyntax GetSingleAssignment(StatementSyntax statement)
    {
        return statement switch
        {
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } => assignment,
            BlockSyntax { Statements.Count: 1 } block
                when block.Statements[0]
                    is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } => assignment,
            _ => throw new InvalidOperationException("Expected a single assignment statement."),
        };
    }
}
