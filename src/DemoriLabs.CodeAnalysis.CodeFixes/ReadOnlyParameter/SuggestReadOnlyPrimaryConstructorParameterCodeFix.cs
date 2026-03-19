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
public sealed class SuggestReadOnlyPrimaryConstructorParameterCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuleIdentifiers.SuggestReadOnlyPrimaryConstructorParameter];

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

        if (node is not ParameterSyntax parameter)
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add [ReadOnly] attribute",
                ct => AddReadOnlyAttributeAsync(context.Document, semanticModel, parameter, ct),
                equivalenceKey: "AddReadOnlyAttribute"
            ),
            diagnostic
        );
    }

    private static async Task<Document> AddReadOnlyAttributeAsync(
        Document document,
        SemanticModel semanticModel,
        ParameterSyntax parameter,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("ReadOnly"));
        var attributeList = SyntaxFactory
            .AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithTrailingTrivia(SyntaxFactory.Space);

        var newParameter = parameter.WithAttributeLists(parameter.AttributeLists.Add(attributeList));

        var newRoot = root.ReplaceNode(parameter, newParameter);
        newRoot = newRoot.EnsureUsingDirective(semanticModel, "DemoriLabs.CodeAnalysis.Attributes");

        return document.WithSyntaxRoot(newRoot);
    }
}
