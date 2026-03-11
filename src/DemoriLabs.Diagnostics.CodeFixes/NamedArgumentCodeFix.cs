using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DemoriLabs.Diagnostics.CodeFixes;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class NamedArgumentCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.NamedArgument];

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

        if (node is not ArgumentSyntax argument)
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null)
            return;

        var operation = semanticModel.GetOperation(argument, context.CancellationToken) as IArgumentOperation;
        if (operation?.Parameter is null)
            return;

        var parameterName = operation.Parameter.Name;

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Add argument name '{parameterName}:'",
                ct => AddArgumentNameAsync(context.Document, argument, parameterName, ct),
                equivalenceKey: $"AddArgumentName_{parameterName}"
            ),
            diagnostic
        );
    }

    private static async Task<Document> AddArgumentNameAsync(
        Document document,
        ArgumentSyntax argument,
        string parameterName,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var nameColon = SyntaxFactory
            .NameColon(SyntaxFactory.IdentifierName(parameterName))
            .WithTrailingTrivia(SyntaxFactory.Space);

        var newArgument = argument.WithNameColon(nameColon);
        var newRoot = root.ReplaceNode(argument, newArgument);

        return document.WithSyntaxRoot(newRoot);
    }
}
