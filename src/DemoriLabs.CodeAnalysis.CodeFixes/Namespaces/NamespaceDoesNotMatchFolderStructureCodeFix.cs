using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.Namespaces;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class NamespaceDoesNotMatchFolderStructureCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuleIdentifiers.NamespaceDoesNotMatchFolderStructure];

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
        if (
            diagnostic.Properties.TryGetValue("ExpectedNamespace", out var expectedNamespace) is false
            || expectedNamespace is null
        )
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Change namespace to '{expectedNamespace}'",
                ct => FixNamespaceAsync(context.Document, node, expectedNamespace, ct),
                equivalenceKey: nameof(NamespaceDoesNotMatchFolderStructureCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixNamespaceAsync(
        Document document,
        SyntaxNode node,
        string expectedNamespace,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var nameNode = node is BaseNamespaceDeclarationSyntax ns ? ns.Name : node;

        var newName = SyntaxFactory
            .ParseName(expectedNamespace)
            .WithLeadingTrivia(nameNode.GetLeadingTrivia())
            .WithTrailingTrivia(nameNode.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(nameNode, newName);
        return document.WithSyntaxRoot(newRoot);
    }
}
