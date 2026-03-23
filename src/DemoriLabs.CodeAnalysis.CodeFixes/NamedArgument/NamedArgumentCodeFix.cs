using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

namespace DemoriLabs.CodeAnalysis.CodeFixes.NamedArgument;

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

        // Transfer leading trivia (indentation) from the expression to the NameColon,
        // since NameColon becomes the first token in the argument.
        var leadingTrivia = argument.Expression.GetLeadingTrivia();
        var nameColon = SyntaxFactory
            .NameColon(SyntaxFactory.IdentifierName(parameterName))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.Space);

        var newArgument = argument.WithNameColon(nameColon).WithExpression(argument.Expression.WithoutLeadingTrivia());
        var newRoot = root.ReplaceNode(argument, newArgument);

        return document.WithSyntaxRoot(newRoot);
    }
}
