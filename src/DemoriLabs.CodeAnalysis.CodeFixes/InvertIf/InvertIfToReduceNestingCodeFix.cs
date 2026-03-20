using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.InvertIf;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class InvertIfToReduceNestingCodeFix : CodeFixProvider
{
    private static readonly SyntaxTrivia NewLine = SyntaxFactory.EndOfLine("\n");

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.InvertIfToReduceNesting];

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

        if (root is null || ifStatement.Statement is not BlockSyntax)
            return document;

        SyntaxList<StatementSyntax> parentStatements;
        SyntaxNode parentNode;
        ExitKind exitKind;

        switch (ifStatement.Parent)
        {
            case BlockSyntax parentBlock:
                parentStatements = parentBlock.Statements;
                parentNode = parentBlock;
                exitKind = DetermineExitKind(parentBlock);
                break;
            case SwitchSectionSyntax switchSection:
                parentStatements = switchSection.Statements;
                parentNode = switchSection;
                exitKind = ExitKind.VoidReturn;
                break;
            default:
                return document;
        }

        var ifIndent = GetIndentationString(ifStatement);
        var ifIndex = parentStatements.IndexOf(ifStatement);

        // Build new statement list
        var newStatements = new List<StatementSyntax>();

        // Keep statements before the if
        for (var i = 0; i < ifIndex; i++)
            newStatements.Add(parentStatements[i]);

        // Determine trailing statements after the if (return/throw/etc.)
        var statementsAfterIf = new List<StatementSyntax>();
        for (var i = ifIndex + 1; i < parentStatements.Count; i++)
            statementsAfterIf.Add(parentStatements[i]);

        // Only collect constant member accesses when there are == or != comparisons
        // with member access operands, to avoid unnecessary semantic model queries.
        var knownConstants = HasMemberAccessComparisons(ifStatement)
            ? CollectConstantMemberAccesses(ifStatement, await document.GetSemanticModelAsync(ct).ConfigureAwait(false))
            : [];

        // Flatten the if statement recursively
        var flattened = FlattenIf(ifStatement, statementsAfterIf, exitKind, ifIndent);
        newStatements.AddRange(flattened);

        if (parentNode is BlockSyntax block)
        {
            var newBlock = block.WithStatements(SyntaxFactory.List(newStatements));
            newBlock = RewriteComparisons(newBlock, knownConstants);
            var newRoot = root.ReplaceNode(block, newBlock);
            return document.WithSyntaxRoot(newRoot);
        }
        else
        {
            var section = (SwitchSectionSyntax)parentNode;
            var newSection = section.WithStatements(SyntaxFactory.List(newStatements));
            newSection = RewriteComparisons(newSection, knownConstants);
            var newRoot = root.ReplaceNode(section, newSection);
            return document.WithSyntaxRoot(newRoot);
        }
    }

    private static List<StatementSyntax> FlattenIf(
        IfStatementSyntax ifStatement,
        List<StatementSyntax> statementsAfterIf,
        ExitKind exitKind,
        string indent
    )
    {
        var result = new List<StatementSyntax>();
        var ifBlock = (BlockSyntax)ifStatement.Statement;

        if (ifStatement.Else is not null)
            FlattenIfElse(ifStatement, statementsAfterIf, exitKind, indent, result);
        else
            FlattenIfWithoutElse(ifStatement, ifBlock, statementsAfterIf, exitKind, indent, result);

        return result;
    }

    private static void FlattenIfWithoutElse(
        IfStatementSyntax ifStatement,
        BlockSyntax ifBlock,
        List<StatementSyntax> statementsAfterIf,
        ExitKind exitKind,
        string indent,
        List<StatementSyntax> result
    )
    {
        var indentTrivia = SyntaxFactory.Whitespace(indent);
        var negatedCondition = NormalizeNegatedCondition(ifStatement.Condition.WithoutTrivia());

        // Determine the guard exit statement
        StatementSyntax guardBody;
        var consumeTrailingExit = false;

        if (statementsAfterIf.Count > 0 && IsExitStatement(statementsAfterIf[0]))
        {
            // Use trailing exit statement as the guard body
            guardBody = BuildGuardFromExit(statementsAfterIf[0], indent);

            // If the if body also ends with an exit, we consume the trailing one
            if (BodyEndsWithExit(ifBlock))
                consumeTrailingExit = true;
        }
        else
        {
            guardBody = BuildExitStatement(exitKind, indent);
        }

        var guardIf = BuildGuardIf(ifStatement.GetLeadingTrivia(), negatedCondition, guardBody);
        result.Add(guardIf);

        // Extract body statements
        AppendBodyStatements(ifBlock.Statements, indent, result, isFirst: true);

        // Add remaining statements after the if
        var trailingStart = consumeTrailingExit ? 1 : 0;
        for (var i = trailingStart; i < statementsAfterIf.Count; i++)
        {
            var leading =
                i == trailingStart
                    ? SyntaxFactory.TriviaList(NewLine, indentTrivia)
                    : SyntaxFactory.TriviaList(indentTrivia);
            result.Add(statementsAfterIf[i].WithLeadingTrivia(leading));
        }

        // Now recursively flatten: check if the last extractable body statement is an invertible if
        TryFlattenNestedIfs(result, exitKind, indent);
    }

    private static void FlattenIfElse(
        IfStatementSyntax ifStatement,
        List<StatementSyntax> statementsAfterIf,
        ExitKind exitKind,
        string indent,
        List<StatementSyntax> result
    )
    {
        var indentTrivia = SyntaxFactory.Whitespace(indent);
        var negatedCondition = NormalizeNegatedCondition(ifStatement.Condition.WithoutTrivia());
        var ifBlock = (BlockSyntax)ifStatement.Statement;

        // Get the else body as the guard
        var elseStatement = ifStatement.Else!.Statement;

        StatementSyntax guardBody;
        switch (elseStatement)
        {
            case BlockSyntax { Statements.Count: 1 } elseBlock:
                guardBody = BuildGuardFromExit(elseBlock.Statements[0], indent);
                break;
            case BlockSyntax { Statements.Count: > 1 } elseBlock2:
                // Multi-statement else: build a block guard
                guardBody = BuildBlockGuard(elseBlock2.Statements, indent);
                break;
            case IfStatementSyntax:
                // else-if chain: flatten all branches into standalone if-blocks
                FlattenIfElseChain(ifStatement, statementsAfterIf, indent, result);
                return;
            default:
                guardBody = BuildGuardFromExit(elseStatement, indent);
                break;
        }

        var guardIf = BuildGuardIf(ifStatement.GetLeadingTrivia(), negatedCondition, guardBody);
        result.Add(guardIf);

        // Extract if body statements
        AppendBodyStatements(ifBlock.Statements, indent, result, isFirst: true);

        // Add trailing statements
        foreach (var t in statementsAfterIf)
            result.Add(t.WithLeadingTrivia(indentTrivia));

        // Recursively flatten nested ifs in the extracted body
        TryFlattenNestedIfs(result, exitKind, indent);
    }

    private static void FlattenIfElseChain(
        IfStatementSyntax ifStatement,
        List<StatementSyntax> statementsAfterIf,
        string indent,
        List<StatementSyntax> result
    )
    {
        var indentTrivia = SyntaxFactory.Whitespace(indent);
        var ifBlock = (BlockSyntax)ifStatement.Statement;

        // Emit the if-branch as a standalone if (keeping original condition)
        if (ifBlock.Statements.Count == 1 && IsExitStatement(ifBlock.Statements[0]))
        {
            // Single exit: emit as guard
            var guardBody = BuildGuardFromExit(ifBlock.Statements[0], indent);
            var leading =
                result.Count > 0 ? SyntaxFactory.TriviaList(NewLine, indentTrivia) : ifStatement.GetLeadingTrivia();
            var guard = BuildGuardIf(leading, ifStatement.Condition.WithoutTrivia().NormalizeWhitespace(), guardBody);
            result.Add(guard);
        }
        else
        {
            // Multi-statement body: keep as if-block without else
            var leading =
                result.Count > 0 ? SyntaxFactory.TriviaList(NewLine, indentTrivia) : ifStatement.GetLeadingTrivia();
            var reindentedBlock = ReindentNode(ifBlock, GetIndentationString(ifBlock), indent);
            var newIf = SyntaxFactory.IfStatement(
                SyntaxFactory
                    .Token(SyntaxKind.IfKeyword)
                    .WithLeadingTrivia(leading)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                ifStatement.Condition.WithoutTrivia().NormalizeWhitespace(),
                SyntaxFactory.Token(SyntaxKind.CloseParenToken),
                reindentedBlock,
                null
            );
            result.Add(newIf);
        }

        // Process else clause
        if (ifStatement.Else is null)
        {
            foreach (var t in statementsAfterIf)
                result.Add(t.WithLeadingTrivia(indentTrivia));
            return;
        }

        switch (ifStatement.Else.Statement)
        {
            case IfStatementSyntax elseIf:
                FlattenIfElseChain(elseIf, statementsAfterIf, indent, result);
                break;
            case BlockSyntax elseBlock:
            {
                // Terminal else: append body statements
                AppendBodyStatements(elseBlock.Statements, indent, result, isFirst: false);

                foreach (var t in statementsAfterIf)
                    result.Add(t.WithLeadingTrivia(indentTrivia));
                break;
            }
        }
    }

    private static void TryFlattenNestedIfs(List<StatementSyntax> statements, ExitKind exitKind, string indent)
    {
        // Look for the pattern: the extracted body ends with (or contains as last stmt) an invertible if
        // We iterate because flattening one may reveal another
        var changed = true;
        while (changed)
        {
            changed = false;

            // Find the last if statement that can be flattened
            // It must be the last statement (for void/continue contexts)
            // or second-to-last followed by an exit (for value-return contexts)
            var lastIdx = statements.Count - 1;
            if (lastIdx < 0)
                break;

            if (
                statements[lastIdx] is IfStatementSyntax { Statement: BlockSyntax { Statements.Count: > 0 } } lastIf
                && (lastIf.Else is not null || exitKind is ExitKind.VoidReturn or ExitKind.Continue)
            )
            {
                var trailing = new List<StatementSyntax>();
                statements.RemoveAt(lastIdx);
                var flattened = FlattenIf(lastIf, trailing, exitKind, indent);
                if (flattened.Count > 0 && statements.Count > 0)
                {
                    flattened[0] = flattened[0].WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(indent));
                }
                statements.AddRange(flattened);
                changed = true;
            }

            // Also check second-to-last + trailing exit
            if (
                !changed
                && lastIdx >= 1
                && statements[lastIdx - 1]
                    is IfStatementSyntax { Statement: BlockSyntax { Statements.Count: > 0 } } secondToLastIf
                && IsExitStatement(statements[lastIdx])
            )
            {
                var trailing = new List<StatementSyntax> { statements[lastIdx] };
                statements.RemoveAt(lastIdx);
                statements.RemoveAt(lastIdx - 1);
                var flattened = FlattenIf(secondToLastIf, trailing, exitKind, indent);
                if (flattened.Count > 0 && statements.Count > 0)
                {
                    flattened[0] = flattened[0].WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(indent));
                }
                statements.AddRange(flattened);
                changed = true;
            }
        }
    }

    private static void AppendBodyStatements(
        SyntaxList<StatementSyntax> bodyStatements,
        string indent,
        List<StatementSyntax> result,
        bool isFirst
    )
    {
        var indentTrivia = SyntaxFactory.Whitespace(indent);

        // Determine original indentation of body statements
        var originalIndent = bodyStatements.Count > 0 ? GetIndentationString(bodyStatements[0]) : indent + "    ";

        for (var i = 0; i < bodyStatements.Count; i++)
        {
            var stmt = bodyStatements[i];

            // Simplify using statements: using (...) { body } → using var ...; body
            if (stmt is UsingStatementSyntax { Declaration: not null } usingStmt)
            {
                var usingDecl = SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.UsingKeyword).WithTrailingTrivia(SyntaxFactory.Space)
                    ),
                    SyntaxFactory.VariableDeclaration(usingStmt.Declaration.Type, usingStmt.Declaration.Variables)
                );

                var leading =
                    isFirst && i == 0
                        ? SyntaxFactory.TriviaList(NewLine, indentTrivia)
                        : SyntaxFactory.TriviaList(indentTrivia);
                result.Add(usingDecl.WithLeadingTrivia(leading));

                // Inline the using body statements
                if (usingStmt.Statement is BlockSyntax usingBlock)
                {
                    foreach (var innerStmt in usingBlock.Statements)
                    {
                        var reindented = ReindentNode(innerStmt, originalIndent + "    ", indent);
                        result.Add(reindented.WithLeadingTrivia(indentTrivia));
                    }
                }

                continue;
            }

            var reindentedStmt = ReindentNode(stmt, originalIndent, indent);

            var stmtLeading =
                isFirst && i == 0
                    ? SyntaxFactory.TriviaList(NewLine, indentTrivia)
                    : SyntaxFactory.TriviaList(indentTrivia);

            result.Add(reindentedStmt.WithLeadingTrivia(stmtLeading));
        }
    }

    private static T ReindentNode<T>(T node, string fromIndent, string toIndent)
        where T : SyntaxNode
    {
        if (fromIndent == toIndent)
        {
            return node;
        }

        var toIndentTrivia = SyntaxFactory.Whitespace(toIndent);

        return node.ReplaceTrivia(
            node.DescendantTrivia(),
            (original, _) =>
            {
                if (!original.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    return original;
                }

                var text = original.ToString();
                if (text == fromIndent)
                {
                    return toIndentTrivia;
                }

                if (text.StartsWith(fromIndent))
                {
                    return SyntaxFactory.Whitespace(toIndent + text.Substring(fromIndent.Length));
                }

                return original;
            }
        );
    }

    private static IfStatementSyntax BuildGuardIf(
        SyntaxTriviaList leadingTrivia,
        ExpressionSyntax negatedCondition,
        StatementSyntax guardBody
    )
    {
        return SyntaxFactory.IfStatement(
            SyntaxFactory
                .Token(SyntaxKind.IfKeyword)
                .WithLeadingTrivia(leadingTrivia)
                .WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.OpenParenToken),
            negatedCondition,
            SyntaxFactory.Token(SyntaxKind.CloseParenToken),
            guardBody,
            null
        );
    }

    private static StatementSyntax BuildGuardFromExit(StatementSyntax exitStmt, string indent)
    {
        var bodyIndent = indent + "    ";

        return exitStmt switch
        {
            ReturnStatementSyntax ret => SyntaxFactory
                .ReturnStatement(ret.Expression?.WithoutTrivia().WithLeadingTrivia(SyntaxFactory.Space))
                .WithReturnKeyword(
                    SyntaxFactory
                        .Token(SyntaxKind.ReturnKeyword)
                        .WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(bodyIndent))
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(NewLine)),

            ThrowStatementSyntax thr => SyntaxFactory
                .ThrowStatement(thr.Expression?.WithoutTrivia().WithLeadingTrivia(SyntaxFactory.Space))
                .WithThrowKeyword(
                    SyntaxFactory
                        .Token(SyntaxKind.ThrowKeyword)
                        .WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(bodyIndent))
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(NewLine)),

            _ => exitStmt.WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(bodyIndent)).WithTrailingTrivia(NewLine),
        };
    }

    private static StatementSyntax BuildBlockGuard(SyntaxList<StatementSyntax> statements, string indent)
    {
        var bodyIndent = indent + "    ";
        var indentTrivia = SyntaxFactory.Whitespace(bodyIndent);

        var newStatements = new List<StatementSyntax>();
        foreach (var stmt in statements)
            newStatements.Add(stmt.WithLeadingTrivia(indentTrivia));

        return SyntaxFactory
            .Block(SyntaxFactory.List(newStatements))
            .WithOpenBraceToken(
                SyntaxFactory
                    .Token(SyntaxKind.OpenBraceToken)
                    .WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(indent))
                    .WithTrailingTrivia(NewLine)
            )
            .WithCloseBraceToken(
                SyntaxFactory
                    .Token(SyntaxKind.CloseBraceToken)
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(indent))
                    .WithTrailingTrivia(NewLine)
            );
    }

    private static StatementSyntax BuildExitStatement(ExitKind exitKind, string indent)
    {
        var bodyIndent = indent + "    ";

        if (exitKind is ExitKind.Continue)
        {
            return SyntaxFactory
                .ContinueStatement()
                .WithContinueKeyword(
                    SyntaxFactory
                        .Token(SyntaxKind.ContinueKeyword)
                        .WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(bodyIndent))
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(NewLine));
        }

        return SyntaxFactory
            .ReturnStatement()
            .WithReturnKeyword(
                SyntaxFactory
                    .Token(SyntaxKind.ReturnKeyword)
                    .WithLeadingTrivia(NewLine, SyntaxFactory.Whitespace(bodyIndent))
            )
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(NewLine));
    }

    private static ExitKind DetermineExitKind(BlockSyntax parentBlock)
    {
        return parentBlock.Parent switch
        {
            ForEachStatementSyntax => ExitKind.Continue,
            ForStatementSyntax => ExitKind.Continue,
            WhileStatementSyntax => ExitKind.Continue,
            DoStatementSyntax => ExitKind.Continue,
            _ => ExitKind.VoidReturn,
        };
    }

    private static bool IsExitStatement(StatementSyntax statement)
    {
        return statement
                is ReturnStatementSyntax
                    or ThrowStatementSyntax
                    or BreakStatementSyntax
                    or ContinueStatementSyntax
            || statement.IsKind(SyntaxKind.YieldBreakStatement);
    }

    private static bool BodyEndsWithExit(BlockSyntax block)
    {
        if (block.Statements.Count == 0)
            return false;

        var last = block.Statements.Last();
        if (IsExitStatement(last))
            return true;

        if (
            last is not IfStatementSyntax { Statement: BlockSyntax ifBlock } ifStmt
            || !BodyEndsWithExit(ifBlock)
            || ifStmt.Else is null
        )
        {
            return false;
        }

        if (ifStmt.Else.Statement is BlockSyntax elseBlock)
            return BodyEndsWithExit(elseBlock);

        return IsExitStatement(ifStmt.Else.Statement);
    }

    private static string GetIndentationString(SyntaxNode node)
    {
        var leadingTrivia = node.GetLeadingTrivia();
        for (var i = leadingTrivia.Count - 1; i >= 0; i--)
        {
            if (leadingTrivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return leadingTrivia[i].ToString();
            }
        }

        return "";
    }

    private static ExpressionSyntax NormalizeNegatedCondition(ExpressionSyntax condition)
    {
        var negated = NegateExpression(condition).NormalizeWhitespace();

        // NormalizeWhitespace doesn't always add space before 'is' keyword
        // (e.g. after closing paren), so fix it up here.
        if (negated is IsPatternExpressionSyntax isPattern)
        {
            negated = isPattern
                .WithExpression(isPattern.Expression.WithTrailingTrivia(SyntaxFactory.Space))
                .WithIsKeyword(isPattern.IsKeyword.WithTrailingTrivia(SyntaxFactory.Space));
        }

        return negated;
    }

    private static ExpressionSyntax NegateExpression(ExpressionSyntax expression)
    {
        if (expression is ParenthesizedExpressionSyntax parens)
            return NegateExpression(parens.Expression);

        switch (expression)
        {
            case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression):
                return prefix.Operand is ParenthesizedExpressionSyntax p ? p.Expression : prefix.Operand;

            case BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.IsExpression) && binary.Right is TypeSyntax typeSyntax:
            {
                // x is Type → x is not Type
                var notPattern = SyntaxFactory.UnaryPattern(
                    SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.TypePattern(typeSyntax.WithoutTrivia())
                );
                return SyntaxFactory.IsPatternExpression(
                    binary.Left.WithoutTrivia(),
                    SyntaxFactory
                        .Token(SyntaxKind.IsKeyword)
                        .WithLeadingTrivia(SyntaxFactory.Space)
                        .WithTrailingTrivia(SyntaxFactory.Space),
                    notPattern
                );
            }

            case BinaryExpressionSyntax binary:
            {
                var negated = NegateComparison(binary);
                if (negated is not null)
                    return negated;

                var deMorgan = NegateDeMorgan(binary);
                if (deMorgan is not null)
                    return deMorgan;
                break;
            }

            case LiteralExpressionSyntax when expression.IsKind(SyntaxKind.TrueLiteralExpression):
                return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);

            case LiteralExpressionSyntax when expression.IsKind(SyntaxKind.FalseLiteralExpression):
                return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);

            case IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax notPat } isExpr
                when notPat.IsKind(SyntaxKind.NotPattern):
                return isExpr.WithPattern(notPat.Pattern.WithLeadingTrivia(notPat.GetLeadingTrivia()));

            case IsPatternExpressionSyntax
            {
                Pattern: ConstantPatternSyntax
                    or TypePatternSyntax
                    or DeclarationPatternSyntax
                    or RecursivePatternSyntax
            } isExpr:
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

        return expression switch
        {
            // Special case: x.HasValue → x is null (for nullable types)
            MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } hasValueAccess =>
                SyntaxFactory.IsPatternExpression(
                    hasValueAccess.Expression.WithoutTrivia(),
                    SyntaxFactory
                        .Token(SyntaxKind.IsKeyword)
                        .WithLeadingTrivia(SyntaxFactory.Space)
                        .WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))
                ),
            // await expressions can't use pattern matching — must use !
            AwaitExpressionSyntax => SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, expression),
            IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax =>
                SyntaxFactory.IsPatternExpression(
                    expression,
                    SyntaxFactory
                        .Token(SyntaxKind.IsKeyword)
                        .WithLeadingTrivia(SyntaxFactory.Space)
                        .WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))
                ),
            _ => SyntaxFactory.PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                SyntaxFactory.ParenthesizedExpression(expression)
            ),
        };
    }

    private static BinaryExpressionSyntax? NegateDeMorgan(BinaryExpressionSyntax binary)
    {
        if (binary.IsKind(SyntaxKind.LogicalAndExpression))
        {
            var left = NegateExpression(binary.Left.WithoutTrivia());
            var right = NegateExpression(binary.Right.WithoutTrivia());

            return SyntaxFactory.BinaryExpression(
                SyntaxKind.LogicalOrExpression,
                MaybeParenthesizeForOr(left),
                SyntaxFactory
                    .Token(SyntaxKind.BarBarToken)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                MaybeParenthesizeForOr(right)
            );
        }

        if (binary.IsKind(SyntaxKind.LogicalOrExpression))
        {
            var left = NegateExpression(binary.Left.WithoutTrivia());
            var right = NegateExpression(binary.Right.WithoutTrivia());

            return SyntaxFactory.BinaryExpression(
                SyntaxKind.LogicalAndExpression,
                MaybeParenthesizeForAnd(left),
                SyntaxFactory
                    .Token(SyntaxKind.AmpersandAmpersandToken)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                MaybeParenthesizeForAnd(right)
            );
        }

        return null;
    }

    private static ExpressionSyntax MaybeParenthesizeForOr(ExpressionSyntax expression)
    {
        return expression;
    }

    private static ExpressionSyntax MaybeParenthesizeForAnd(ExpressionSyntax expression)
    {
        if (expression is BinaryExpressionSyntax bin && bin.IsKind(SyntaxKind.LogicalOrExpression))
            return SyntaxFactory.ParenthesizedExpression(expression);

        return expression;
    }

    private static ExpressionSyntax? NegateComparison(BinaryExpressionSyntax binary)
    {
        // For ==/!= with constants, use pattern matching
        if (binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            var (target, constant) = GetComparisonTargetAndConstant(binary);
            if (target is not null && constant is not null)
            {
                if (binary.Kind() is SyntaxKind.EqualsExpression)
                {
                    // Negating x == C → x is not C
                    return SyntaxFactory.IsPatternExpression(
                        target,
                        SyntaxFactory.Token(SyntaxKind.IsKeyword),
                        SyntaxFactory.UnaryPattern(
                            SyntaxFactory.Token(SyntaxKind.NotKeyword),
                            SyntaxFactory.ConstantPattern(constant)
                        )
                    );
                }

                // Negating x != C → x is C
                return SyntaxFactory.IsPatternExpression(
                    target,
                    SyntaxFactory.Token(SyntaxKind.IsKeyword),
                    SyntaxFactory.ConstantPattern(constant)
                );
            }
        }

        var (newExprKind, newOpKind) = binary.Kind() switch
        {
            SyntaxKind.EqualsExpression => (SyntaxKind.NotEqualsExpression, SyntaxKind.ExclamationEqualsToken),
            SyntaxKind.NotEqualsExpression => (SyntaxKind.EqualsExpression, SyntaxKind.EqualsEqualsToken),
            SyntaxKind.GreaterThanExpression => (SyntaxKind.LessThanOrEqualExpression, SyntaxKind.LessThanEqualsToken),
            SyntaxKind.LessThanExpression => (
                SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxKind.GreaterThanEqualsToken
            ),
            SyntaxKind.GreaterThanOrEqualExpression => (SyntaxKind.LessThanExpression, SyntaxKind.LessThanToken),
            SyntaxKind.LessThanOrEqualExpression => (SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanToken),
            _ => (SyntaxKind.None, SyntaxKind.None),
        };

        if (newExprKind is SyntaxKind.None)
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

    private static bool IsConstantLike(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax;
    }

    private static (ExpressionSyntax? target, ExpressionSyntax? constant) GetComparisonTargetAndConstant(
        BinaryExpressionSyntax binary
    )
    {
        if (IsConstantLike(binary.Right))
            return (binary.Left.WithoutTrivia(), binary.Right.WithoutTrivia());
        if (IsConstantLike(binary.Left))
            return (binary.Right.WithoutTrivia(), binary.Left.WithoutTrivia());
        return (null, null);
    }

    private static bool HasMemberAccessComparisons(SyntaxNode node)
    {
        foreach (var binary in node.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (binary.Kind() is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression))
            {
                continue;
            }

            if (
                binary.Left is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax }
                || binary.Right is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax }
            )
            {
                return true;
            }
        }

        return false;
    }

    private static T RewriteComparisons<T>(T node, HashSet<string> knownConstants)
        where T : SyntaxNode
    {
        // Skip the rewriter entirely if no == or != comparisons exist in the output
        foreach (var descendant in node.DescendantNodes())
        {
            if (
                descendant is not BinaryExpressionSyntax b
                || b.Kind() is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
            )
            {
                continue;
            }

            var rewriter = new ComparisonToPatternRewriter(knownConstants);
            return (T)rewriter.Visit(node);
        }

        return node;
    }

    private static HashSet<string> CollectConstantMemberAccesses(SyntaxNode node, SemanticModel? semanticModel)
    {
        var result = new HashSet<string>();
        if (semanticModel is null)
            return result;

        // Only check member accesses that are operands of == or != comparisons,
        // since the ComparisonToPatternRewriter only rewrites those operators.
        foreach (var binary in node.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (binary.Kind() is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression))
            {
                continue;
            }

            TryAddConstantMemberAccess(binary.Left, semanticModel, result);
            TryAddConstantMemberAccess(binary.Right, semanticModel, result);
        }

        return result;
    }

    private static void TryAddConstantMemberAccess(
        ExpressionSyntax expr,
        SemanticModel semanticModel,
        HashSet<string> result
    )
    {
        if (
            expr is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax } memberAccess
            && semanticModel.GetConstantValue(memberAccess).HasValue
        )
        {
            result.Add(memberAccess.WithoutTrivia().ToFullString());
        }
    }

    private sealed class ComparisonToPatternRewriter(HashSet<string> knownConstants) : CSharpSyntaxRewriter
    {
        // Do not rewrite comparisons inside lambda expressions — they may be
        // converted to expression trees (e.g. IQueryable), which do not support
        // pattern-matching 'is' expressions.
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) =>
            node;

        private bool IsConstant(ExpressionSyntax expression)
        {
            return expression switch
            {
                LiteralExpressionSyntax => true,
                MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax } => knownConstants.Contains(
                    expression.WithoutTrivia().ToFullString()
                ),
                _ => false,
            };
        }

        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var visited = (BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;

            if (visited.Kind() is not (SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression))
                return visited;

            ExpressionSyntax target;
            ExpressionSyntax constant;

            if (IsConstant(visited.Right))
            {
                target = visited.Left.WithoutTrivia();
                constant = visited.Right.WithoutTrivia();
            }
            else if (IsConstant(visited.Left))
            {
                target = visited.Right.WithoutTrivia();
                constant = visited.Left.WithoutTrivia();
            }
            else
            {
                return visited;
            }

            var isKeyword = SyntaxFactory
                .Token(SyntaxKind.IsKeyword)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(SyntaxFactory.Space);

            if (visited.Kind() is SyntaxKind.EqualsExpression)
            {
                // For member access constants (enums), use BinaryExpression(IsExpression)
                // with QualifiedName to match the parser's syntax tree output
                if (
                    constant
                    is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax typeName } memberAccess
                )
                {
                    return SyntaxFactory.IsPatternExpression(
                        target,
                        isKeyword,
                        SyntaxFactory.ConstantPattern(constant)
                    );
                }

                var qualifiedName = SyntaxFactory.QualifiedName(
                    typeName.WithoutTrivia(),
                    memberAccess.Name.WithoutTrivia()
                );

                return SyntaxFactory.BinaryExpression(SyntaxKind.IsExpression, target, isKeyword, qualifiedName);
            }

            return SyntaxFactory.IsPatternExpression(
                target,
                isKeyword,
                SyntaxFactory.UnaryPattern(
                    SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.ConstantPattern(constant)
                )
            );
        }
    }

    private enum ExitKind
    {
        VoidReturn,
        Continue,
    }
}
