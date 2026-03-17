using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class ReadOnlyIncompatibleModifierCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.ReadOnlyIncompatibleModifier];

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

        if (node is not AttributeSyntax attribute)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove [ReadOnly] attribute",
                ct => RemoveAttributeAsync(context.Document, attribute, ct),
                equivalenceKey: "RemoveReadOnlyAttribute"
            ),
            diagnostic
        );
    }

    private static async Task<Document> RemoveAttributeAsync(
        Document document,
        AttributeSyntax attribute,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var attrList = (AttributeListSyntax)attribute.Parent!;

        SyntaxNode newRoot;

        if (attrList.Attributes.Count == 1)
        {
            newRoot = root.RemoveNode(attrList, SyntaxRemoveOptions.KeepLeadingTrivia)!;
        }
        else
        {
            var newAttrList = attrList.RemoveNode(attribute, SyntaxRemoveOptions.KeepLeadingTrivia)!;
            newRoot = root.ReplaceNode(attrList, newAttrList);
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
