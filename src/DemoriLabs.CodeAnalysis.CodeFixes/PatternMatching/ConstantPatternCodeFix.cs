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
public sealed class ConstantPatternCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseConstantPattern];

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

        while (
            node
                is not null
                    and not BinaryExpressionSyntax
                    {
                        RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression,
                    }
        )
        {
            node = node.Parent;
        }

        if (node is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use constant pattern",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(ConstantPatternCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null)
            return document;

        if (node is not BinaryExpressionSyntax binaryExpression)
            return document;

        var left = binaryExpression.Left;
        var right = binaryExpression.Right;

        var leftIsConstant = IsConstant(left, semanticModel, ct);

        var variable = leftIsConstant ? right : left;
        var constant = leftIsConstant ? left : right;

        var isEquals = binaryExpression.IsKind(SyntaxKind.EqualsExpression);

        var variableText = variable.WithoutTrivia().ToFullString();
        var constantText = constant.WithoutTrivia().ToFullString();

        var replacementText = isEquals ? $"{variableText} is {constantText}" : $"{variableText} is not {constantText}";

        // Parse to get the correct syntax tree structure that matches the parser's output
        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(binaryExpression.GetLeadingTrivia())
            .WithTrailingTrivia(binaryExpression.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(binaryExpression, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsConstant(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken ct)
    {
        while (expression is ParenthesizedExpressionSyntax paren)
            expression = paren.Expression;

        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
            return true;

        if (expression is LiteralExpressionSyntax)
            return true;

        if (
            expression is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } prefix
            && prefix.Operand is LiteralExpressionSyntax
        )
        {
            return true;
        }

        var constantValue = semanticModel.GetConstantValue(expression, ct);
        return constantValue.HasValue;
    }
}
