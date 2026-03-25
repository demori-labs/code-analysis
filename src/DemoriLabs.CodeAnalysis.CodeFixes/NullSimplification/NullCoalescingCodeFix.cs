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
public sealed class NullCoalescingCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseNullCoalescing];

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

        switch (token.Parent)
        {
            case ConditionalExpressionSyntax conditional:
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Use null-coalescing operator",
                        ct => FixTernaryAsync(context.Document, conditional, ct),
                        equivalenceKey: nameof(NullCoalescingCodeFix)
                    ),
                    diagnostic
                );
                break;
            case IfStatementSyntax ifStatement:
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Use null-coalescing operator",
                        ct => FixIfStatementAsync(context.Document, ifStatement, ct),
                        equivalenceKey: nameof(NullCoalescingCodeFix)
                    ),
                    diagnostic
                );
                break;
        }
    }

    private static async Task<Document> FixTernaryAsync(
        Document document,
        ConditionalExpressionSyntax conditional,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var (variable, isNotNull) = GetNullCheckVariable(conditional.Condition);
        var variableText = variable.WithoutTrivia().ToFullString();

        var fallbackExpr = isNotNull
            ? conditional.WhenFalse.WithoutTrivia().ToFullString()
            : conditional.WhenTrue.WithoutTrivia().ToFullString();

        var replacementText = $"{variableText} ?? {fallbackExpr}";
        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(conditional.GetLeadingTrivia())
            .WithTrailingTrivia(conditional.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(conditional, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> FixIfStatementAsync(
        Document document,
        IfStatementSyntax ifStatement,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var nullCheck = GetNullCheckVariable(ifStatement.Condition);
        var variableText = nullCheck.Variable.WithoutTrivia().ToFullString();

        string fallbackText;
        SyntaxNode newRoot;

        if (ifStatement.Else is not null)
        {
            var elseReturnExpr = GetSingleReturnExpression(ifStatement.Else.Statement);
            fallbackText = elseReturnExpr.WithoutTrivia().ToFullString();

            var replacementText = $"return {variableText} ?? {fallbackText};";
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
            fallbackText = nextReturnExpr.WithoutTrivia().ToFullString();

            var replacementText = $"return {variableText} ?? {fallbackText};";
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

    private static (ExpressionSyntax Variable, bool IsNotNull) GetNullCheckVariable(ExpressionSyntax condition)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax binary:
            {
                if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                {
                    return IsNullLiteral(binary.Right)
                        ? (binary.Left, IsNotNull: true)
                        : (binary.Right, IsNotNull: true);
                }

                return IsNullLiteral(binary.Right) ? (binary.Left, IsNotNull: false) : (binary.Right, IsNotNull: false);
            }
            case IsPatternExpressionSyntax isPattern:
            {
                return isPattern.Pattern is UnaryPatternSyntax { OperatorToken.RawKind: (int)SyntaxKind.NotKeyword }
                    ? (isPattern.Expression, IsNotNull: true)
                    : (isPattern.Expression, IsNotNull: false);
            }
            default:
                throw new InvalidOperationException("Expected a null check condition.");
        }
    }

    private static bool IsNullLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
    }
}
