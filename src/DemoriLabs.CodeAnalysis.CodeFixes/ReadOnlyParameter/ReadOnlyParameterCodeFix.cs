using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.ReadOnlyParameter;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class ReadOnlyParameterCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.ReadOnlyParameter];

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

        if (node.Parent is not ExpressionStatementSyntax expressionStatement)
            return;

        if (expressionStatement.Parent is not BlockSyntax)
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null)
            return;

        var parameterName = GetParameterName(node);
        if (parameterName is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Introduce local variable for '{parameterName}'",
                ct =>
                    IntroduceLocalVariableAsync(
                        context.Document,
                        semanticModel,
                        node,
                        expressionStatement,
                        parameterName,
                        ct
                    ),
                equivalenceKey: $"ReadOnlyParameterFix_{parameterName}"
            ),
            diagnostic
        );
    }

    private static string? GetParameterName(SyntaxNode node)
    {
        return node switch
        {
            AssignmentExpressionSyntax a => (a.Left as IdentifierNameSyntax)?.Identifier.Text,
            PostfixUnaryExpressionSyntax p => (p.Operand as IdentifierNameSyntax)?.Identifier.Text,
            PrefixUnaryExpressionSyntax p => (p.Operand as IdentifierNameSyntax)?.Identifier.Text,
            _ => null,
        };
    }

    private static ExpressionSyntax GetTargetExpression(SyntaxNode node)
    {
        return node switch
        {
            AssignmentExpressionSyntax a => a.Left,
            PostfixUnaryExpressionSyntax p => p.Operand,
            PrefixUnaryExpressionSyntax p => p.Operand,
            _ => throw new InvalidOperationException("Unexpected node kind"),
        };
    }

    private static async Task<Document> IntroduceLocalVariableAsync(
        Document document,
        SemanticModel semanticModel,
        SyntaxNode assignmentNode,
        ExpressionStatementSyntax expressionStatement,
        string parameterName,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var block = (BlockSyntax)expressionStatement.Parent!;
        var statementIndex = block.Statements.IndexOf(expressionStatement);

        var newVarName = GetUniqueName(parameterName, block);
        var initExpression = BuildInitializerExpression(assignmentNode, parameterName);

        var varDeclaration = SyntaxFactory
            .LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName(
                        SyntaxFactory.Identifier(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.VarKeyword,
                            "var",
                            "var",
                            SyntaxFactory.TriviaList(SyntaxFactory.Space)
                        )
                    ),
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory
                            .VariableDeclarator(newVarName)
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory
                                        .Token(SyntaxKind.EqualsToken)
                                        .WithLeadingTrivia(SyntaxFactory.Space)
                                        .WithTrailingTrivia(SyntaxFactory.Space),
                                    initExpression
                                )
                            )
                    )
                )
            )
            .WithTriviaFrom(expressionStatement);

        var targetExpression = GetTargetExpression(assignmentNode);
        var parameterSymbol = semanticModel.GetSymbolInfo(targetExpression, ct).Symbol;

        var newStatements = new List<StatementSyntax>(block.Statements.Count);

        for (var i = 0; i < block.Statements.Count; i++)
        {
            if (i == statementIndex)
            {
                newStatements.Add(varDeclaration);
                continue;
            }

            if (i > statementIndex && parameterSymbol is not null)
            {
                var stmt = block.Statements[i];
                var refs = stmt.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(id =>
                        SymbolEqualityComparer.Default.Equals(
                            semanticModel.GetSymbolInfo(id, ct).Symbol,
                            parameterSymbol
                        )
                    )
                    .ToList();

                if (refs.Count > 0)
                {
                    stmt = stmt.ReplaceNodes(
                        refs,
                        (orig, _) => SyntaxFactory.IdentifierName(newVarName).WithTriviaFrom(orig)
                    );
                }

                newStatements.Add(stmt);
                continue;
            }

            newStatements.Add(block.Statements[i]);
        }

        var newBlock = block.WithStatements(SyntaxFactory.List(newStatements));
        var newRoot = root.ReplaceNode(block, newBlock);
        return document.WithSyntaxRoot(newRoot);
    }

    private static ExpressionSyntax BuildInitializerExpression(SyntaxNode node, string paramName)
    {
        var paramRef = SyntaxFactory.IdentifierName(paramName);
        var one = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1));

        return node switch
        {
            AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } a => a.Right,

            AssignmentExpressionSyntax compound => SyntaxFactory.BinaryExpression(
                CompoundToBinaryKind(compound.Kind()),
                paramRef.WithTrailingTrivia(SyntaxFactory.Space),
                compound.Right.WithLeadingTrivia(SyntaxFactory.Space)
            ),

            PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression } =>
                SyntaxFactory.BinaryExpression(
                    SyntaxKind.AddExpression,
                    paramRef.WithTrailingTrivia(SyntaxFactory.Space),
                    one.WithLeadingTrivia(SyntaxFactory.Space)
                ),

            PostfixUnaryExpressionSyntax => SyntaxFactory.BinaryExpression(
                SyntaxKind.SubtractExpression,
                paramRef.WithTrailingTrivia(SyntaxFactory.Space),
                one.WithLeadingTrivia(SyntaxFactory.Space)
            ),

            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression } =>
                SyntaxFactory.BinaryExpression(
                    SyntaxKind.AddExpression,
                    paramRef.WithTrailingTrivia(SyntaxFactory.Space),
                    one.WithLeadingTrivia(SyntaxFactory.Space)
                ),

            _ => SyntaxFactory.BinaryExpression(
                SyntaxKind.SubtractExpression,
                paramRef.WithTrailingTrivia(SyntaxFactory.Space),
                one.WithLeadingTrivia(SyntaxFactory.Space)
            ),
        };
    }

    private static SyntaxKind CompoundToBinaryKind(SyntaxKind kind)
    {
        return kind switch
        {
            SyntaxKind.AddAssignmentExpression => SyntaxKind.AddExpression,
            SyntaxKind.SubtractAssignmentExpression => SyntaxKind.SubtractExpression,
            SyntaxKind.MultiplyAssignmentExpression => SyntaxKind.MultiplyExpression,
            SyntaxKind.DivideAssignmentExpression => SyntaxKind.DivideExpression,
            SyntaxKind.ModuloAssignmentExpression => SyntaxKind.ModuloExpression,
            SyntaxKind.AndAssignmentExpression => SyntaxKind.BitwiseAndExpression,
            SyntaxKind.OrAssignmentExpression => SyntaxKind.BitwiseOrExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression => SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.LeftShiftAssignmentExpression => SyntaxKind.LeftShiftExpression,
            SyntaxKind.RightShiftAssignmentExpression => SyntaxKind.RightShiftExpression,
            SyntaxKind.UnsignedRightShiftAssignmentExpression => SyntaxKind.UnsignedRightShiftExpression,
            SyntaxKind.CoalesceAssignmentExpression => SyntaxKind.CoalesceExpression,
            _ => SyntaxKind.AddExpression,
        };
    }

    private static string GetUniqueName(string baseName, BlockSyntax block)
    {
        var existing = block
            .DescendantTokens()
            .Where(t => t.IsKind(SyntaxKind.IdentifierToken))
            .Select(t => t.Text)
            .ToImmutableHashSet();

        var candidate = baseName + "Local";
        var suffix = 1;
        while (existing.Contains(candidate))
            candidate = baseName + "Local" + suffix++;

        return candidate;
    }
}
