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
public sealed class TypeCheckAndCastCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseDeclarationPatternInsteadOfCast];

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

        if (node is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression })
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use declaration pattern",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(TypeCheckAndCastCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null || node is not BinaryExpressionSyntax isExpression)
            return document;

        var checkedExpression = isExpression.Left;
        var checkedType = isExpression.Right;

        var ifStatement = FindEnclosingIf(isExpression);
        if (ifStatement is null)
            return document;

        var typeName = checkedType.WithoutTrivia().ToString();
        var varName = GenerateVariableName(typeName);

        var checkedExpressionText = checkedExpression.WithoutTrivia().ToString();
        var casts = ifStatement
            .Statement.DescendantNodes()
            .OfType<CastExpressionSyntax>()
            .Where(c =>
                string.Equals(c.Type.WithoutTrivia().ToString(), typeName, StringComparison.Ordinal)
                && string.Equals(
                    c.Expression.WithoutTrivia().ToString(),
                    checkedExpressionText,
                    StringComparison.Ordinal
                )
            )
            .ToList();

        var newRoot = root.ReplaceNodes(
            casts,
            (original, _) =>
                SyntaxFactory
                    .IdentifierName(varName)
                    .WithLeadingTrivia(original.GetLeadingTrivia())
                    .WithTrailingTrivia(original.GetTrailingTrivia())
        );

        var updatedIsExpression = newRoot.FindNode(isExpression.Span);

        var replacementText = $"{checkedExpression.WithoutTrivia().ToFullString()} is {typeName} {varName}";
        var replacementExpression = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(updatedIsExpression.GetLeadingTrivia())
            .WithTrailingTrivia(updatedIsExpression.GetTrailingTrivia());

        newRoot = newRoot.ReplaceNode(updatedIsExpression, replacementExpression);

        return document.WithSyntaxRoot(newRoot);
    }

    private static IfStatementSyntax? FindEnclosingIf(BinaryExpressionSyntax isExpression)
    {
        var current = isExpression.Parent;
        while (
            current
                is ParenthesizedExpressionSyntax
                    or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression }
        )
        {
            current = current.Parent;
        }

        return current as IfStatementSyntax;
    }

    private static string GenerateVariableName(string typeName)
    {
        var lastDot = typeName.LastIndexOf('.');
        var simpleName = lastDot >= 0 ? typeName.Substring(lastDot + 1) : typeName;

        var angleBracket = simpleName.IndexOf('<');
        if (angleBracket >= 0)
            simpleName = simpleName.Substring(0, angleBracket);

        if (simpleName.Length > 0 && char.IsUpper(simpleName[0]))
        {
            simpleName = char.ToLowerInvariant(simpleName[0]) + simpleName.Substring(1);
        }

        // Prefix C# keywords with @
        if (SyntaxFacts.GetKeywordKind(simpleName) != SyntaxKind.None)
        {
            simpleName = "@" + simpleName;
        }

        return simpleName;
    }
}
