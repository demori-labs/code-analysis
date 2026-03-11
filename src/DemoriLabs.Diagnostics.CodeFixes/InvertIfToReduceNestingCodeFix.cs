using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.Diagnostics.CodeFixes;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class InvertIfToReduceNestingCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuleIdentifiers.InvertIfToReduceNesting];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);

        if (token.Parent is not IfStatementSyntax ifStatement)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Invert 'if' to reduce nesting",
                ct => InvertIfAsync(context.Document, ifStatement, ct),
                equivalenceKey: "InvertIfToReduceNesting"
            ),
            diagnostic
        );
    }

    private static async Task<Document> InvertIfAsync(
        Document document,
        IfStatementSyntax ifStatement,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        if (ifStatement.Statement is not BlockSyntax ifBlock)
            return document;

        if (ifStatement.Parent is not BlockSyntax parentBlock)
            return document;

        var negatedCondition = NegateExpression(ifStatement.Condition.WithoutTrivia())
            .NormalizeWhitespace();

        var ifIndent = GetIndentationString(ifStatement);
        var returnIndent = ifBlock.Statements.Count > 0
            ? GetIndentationString(ifBlock.Statements[0])
            : ifIndent + "    ";

        // Detect "followed by return" pattern
        var ifIndex = parentBlock.Statements.IndexOf(ifStatement);
        ReturnStatementSyntax? followingReturn = null;

        if (ifIndex < parentBlock.Statements.Count - 1
            && parentBlock.Statements[ifIndex + 1] is ReturnStatementSyntax ret)
        {
            followingReturn = ret;
        }

        // Build the guard return statement (plain return; or return <expr>;)
        var returnExpression = followingReturn?.Expression?
            .WithoutTrivia()
            .WithLeadingTrivia(SyntaxFactory.Space);

        var guardReturnStatement = SyntaxFactory
            .ReturnStatement(returnExpression)
            .WithReturnKeyword(
                SyntaxFactory
                    .Token(SyntaxKind.ReturnKeyword)
                    .WithLeadingTrivia(
                        SyntaxFactory.EndOfLine("\n"),
                        SyntaxFactory.Whitespace(returnIndent)
                    )
            )
            .WithSemicolonToken(
                SyntaxFactory
                    .Token(SyntaxKind.SemicolonToken)
                    .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"))
            );

        // Build guard if with explicit trivia
        var guardIf = SyntaxFactory.IfStatement(
            SyntaxFactory
                .Token(SyntaxKind.IfKeyword)
                .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.OpenParenToken),
            negatedCondition,
            SyntaxFactory.Token(SyntaxKind.CloseParenToken),
            guardReturnStatement,
            null
        );

        // Build new statement list
        var newStatements = new List<StatementSyntax>();

        for (var i = 0; i < ifIndex; i++)
            newStatements.Add(parentBlock.Statements[i]);

        newStatements.Add(guardIf);

        // Extract body statements at the if-statement's indentation level
        var ifIndentTrivia = SyntaxFactory.Whitespace(ifIndent);

        for (var i = 0; i < ifBlock.Statements.Count; i++)
        {
            var stmt = ifBlock.Statements[i];

            var leading =
                i == 0
                    ? SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine("\n"), ifIndentTrivia)
                    : SyntaxFactory.TriviaList(ifIndentTrivia);

            newStatements.Add(stmt.WithLeadingTrivia(leading));
        }

        // Add remaining statements after the if (skip the following return if body ends with one)
        var bodyEndsWithReturn = followingReturn is not null
            && ifBlock.Statements.Last() is ReturnStatementSyntax;
        var afterIfStart = ifIndex + 1 + (bodyEndsWithReturn ? 1 : 0);

        for (var i = afterIfStart; i < parentBlock.Statements.Count; i++)
        {
            var stmt = parentBlock.Statements[i];

            // Adjust leading trivia for the kept return to remove the original blank line
            if (followingReturn is not null && stmt == followingReturn)
                stmt = stmt.WithLeadingTrivia(ifIndentTrivia);

            newStatements.Add(stmt);
        }

        var newBlock = parentBlock.WithStatements(SyntaxFactory.List(newStatements));
        var newRoot = root.ReplaceNode(parentBlock, newBlock);
        return document.WithSyntaxRoot(newRoot);
    }

    private static string GetIndentationString(SyntaxNode node)
    {
        return node.GetLeadingTrivia()
                .Reverse()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)) is { } trivia
            && trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                ? trivia.ToString()
                : "";
    }

    internal static ExpressionSyntax NegateExpression(ExpressionSyntax expression)
    {
        if (expression is ParenthesizedExpressionSyntax parens)
            return NegateExpression(parens.Expression);

        switch (expression)
        {
            case PrefixUnaryExpressionSyntax prefix
                when prefix.IsKind(SyntaxKind.LogicalNotExpression):
                return prefix.Operand is ParenthesizedExpressionSyntax p
                    ? p.Expression
                    : prefix.Operand;

            case BinaryExpressionSyntax binary:
            {
                var negated = NegateComparison(binary);
                if (negated is not null)
                    return negated;
                break;
            }

            case LiteralExpressionSyntax when expression.IsKind(SyntaxKind.TrueLiteralExpression):
                return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

            case LiteralExpressionSyntax
                when expression.IsKind(SyntaxKind.FalseLiteralExpression):
                return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);

            case IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax notPat } isExpr
                when notPat.IsKind(SyntaxKind.NotPattern):
                return isExpr.WithPattern(
                    notPat.Pattern.WithLeadingTrivia(notPat.GetLeadingTrivia())
                );

            case IsPatternExpressionSyntax isExpr
                when isExpr.Pattern is ConstantPatternSyntax or TypePatternSyntax:
            {
                var notPattern = SyntaxFactory.UnaryPattern(
                    SyntaxFactory
                        .Token(SyntaxKind.NotKeyword)
                        .WithLeadingTrivia(isExpr.Pattern.GetLeadingTrivia())
                        .WithTrailingTrivia(SyntaxFactory.Space),
                    isExpr.Pattern.WithoutLeadingTrivia()
                );
                return isExpr.WithPattern(notPattern);
            }
        }

        if (
            expression
                is IdentifierNameSyntax
                    or MemberAccessExpressionSyntax
                    or InvocationExpressionSyntax
        )
        {
            return SyntaxFactory.PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                expression
            );
        }

        return SyntaxFactory.PrefixUnaryExpression(
            SyntaxKind.LogicalNotExpression,
            SyntaxFactory.ParenthesizedExpression(expression)
        );
    }

    private static BinaryExpressionSyntax? NegateComparison(BinaryExpressionSyntax binary)
    {
        var (newExprKind, newOpKind) = binary.Kind() switch
        {
            SyntaxKind.EqualsExpression
                => (SyntaxKind.NotEqualsExpression, SyntaxKind.ExclamationEqualsToken),
            SyntaxKind.NotEqualsExpression
                => (SyntaxKind.EqualsExpression, SyntaxKind.EqualsEqualsToken),
            SyntaxKind.GreaterThanExpression
                => (SyntaxKind.LessThanOrEqualExpression, SyntaxKind.LessThanEqualsToken),
            SyntaxKind.LessThanExpression
                => (SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.GreaterThanEqualsToken),
            SyntaxKind.GreaterThanOrEqualExpression
                => (SyntaxKind.LessThanExpression, SyntaxKind.LessThanToken),
            SyntaxKind.LessThanOrEqualExpression
                => (SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanToken),
            _ => (SyntaxKind.None, SyntaxKind.None),
        };

        if (newExprKind == SyntaxKind.None)
            return null;

        return SyntaxFactory.BinaryExpression(
            newExprKind,
            binary.Left,
            SyntaxFactory
                .Token(newOpKind)
                .WithLeadingTrivia(binary.OperatorToken.LeadingTrivia)
                .WithTrailingTrivia(binary.OperatorToken.TrailingTrivia),
            binary.Right
        );
    }
}
