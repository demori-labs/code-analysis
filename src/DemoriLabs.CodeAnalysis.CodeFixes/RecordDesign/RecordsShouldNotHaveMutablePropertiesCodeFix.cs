using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.RecordDesign;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class RecordsShouldNotHaveMutablePropertiesCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuleIdentifiers.RecordsShouldNotHaveMutableProperties];

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
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();

        var setAccessor = property?.AccessorList?.Accessors.FirstOrDefault(a =>
            a.IsKind(SyntaxKind.SetAccessorDeclaration)
        );

        if (setAccessor is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace 'set' with 'init'",
                _ => ReplaceSetWithInitAsync(context.Document, root, setAccessor),
                equivalenceKey: "ReplaceSetWithInit"
            ),
            diagnostic
        );
    }

    private static Task<Document> ReplaceSetWithInitAsync(
        Document document,
        SyntaxNode root,
        AccessorDeclarationSyntax setAccessor
    )
    {
        var initAccessor = SyntaxFactory
            .AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithModifiers(setAccessor.Modifiers)
            .WithBody(setAccessor.Body)
            .WithExpressionBody(setAccessor.ExpressionBody)
            .WithSemicolonToken(setAccessor.SemicolonToken)
            .WithLeadingTrivia(setAccessor.GetLeadingTrivia())
            .WithTrailingTrivia(setAccessor.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(setAccessor, initAccessor);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
