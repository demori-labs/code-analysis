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
public sealed class AsWithNullCheckCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseDeclarationPatternInsteadOfAs];

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
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: false);

        while (node is not null and not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression })
            node = node.Parent;

        if (node is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use declaration pattern",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(AsWithNullCheckCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        if (node is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpression)
            return document;

        if (asExpression.Parent?.Parent?.Parent?.Parent is not LocalDeclarationStatementSyntax localDeclaration)
            return document;

        var variable = localDeclaration.Declaration.Variables[0];

        var variableName = variable.Identifier.Text;
        var sourceExpression = asExpression.Left.WithoutTrivia().ToFullString();
        var targetType = asExpression.Right.WithoutTrivia().ToFullString();

        if (localDeclaration.Parent is not BlockSyntax block)
            return document;

        var statements = block.Statements;
        var declarationIndex = statements.IndexOf(localDeclaration);
        if (declarationIndex + 1 >= statements.Count)
            return document;

        if (statements[declarationIndex + 1] is not IfStatementSyntax ifStatement)
            return document;

        var isGuardClause = IsPositiveNullCheck(ifStatement.Condition);
        var newConditionText = isGuardClause
            ? $"{sourceExpression} is not {targetType} {variableName}"
            : $"{sourceExpression} is {targetType} {variableName}";
        var newCondition = SyntaxFactory.ParseExpression(newConditionText);

        var newIfStatement = ifStatement
            .WithCondition(
                newCondition
                    .WithLeadingTrivia(ifStatement.Condition.GetLeadingTrivia())
                    .WithTrailingTrivia(ifStatement.Condition.GetTrailingTrivia())
            )
            .WithLeadingTrivia(localDeclaration.GetLeadingTrivia());

        var newStatements = statements
            .RemoveAt(declarationIndex)
            .RemoveAt(declarationIndex) // if-statement is now at declarationIndex after removal
            .Insert(declarationIndex, newIfStatement);

        var newBlock = block.WithStatements(newStatements);
        var newRoot = root.ReplaceNode(block, newBlock);

        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsPositiveNullCheck(ExpressionSyntax condition)
    {
        return condition switch
        {
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } => true,
            IsPatternExpressionSyntax
            {
                Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression },
            } => true,
            _ => false,
        };
    }
}
