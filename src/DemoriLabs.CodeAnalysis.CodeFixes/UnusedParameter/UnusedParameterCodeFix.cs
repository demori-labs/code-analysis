using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DemoriLabs.CodeAnalysis.CodeFixes.UnusedParameter;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class UnusedParameterCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UnusedParameter];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
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

        var parameterName = parameter.Identifier.Text;

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Remove unused parameter '{parameterName}'",
                ct => RemoveParameterAsync(context.Document, parameter, ct),
                equivalenceKey: nameof(UnusedParameterCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Solution> RemoveParameterAsync(
        Document document,
        ParameterSyntax parameter,
        CancellationToken ct
    )
    {
        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (
            semanticModel?.GetDeclaredSymbol(parameter, ct)
            is not IParameterSymbol { ContainingSymbol: IMethodSymbol methodSymbol } parameterSymbol
        )
        {
            return document.Project.Solution;
        }

        var parameterIndex = -1;
        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[i], parameterSymbol))
            {
                parameterIndex = i;
                break;
            }
        }

        if (parameterIndex < 0)
            return document.Project.Solution;

        var solution = document.Project.Solution;

        var references = await SymbolFinder.FindReferencesAsync(methodSymbol, solution, ct).ConfigureAwait(false);

        var documentEdits = new Dictionary<DocumentId, List<SyntaxNode>>();

        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                CollectArgumentToRemove(documentEdits, location, parameterIndex);
            }
        }

        // Also collect the parameter declaration itself
        if (!documentEdits.TryGetValue(document.Id, out var declEdits))
        {
            declEdits = [];
            documentEdits[document.Id] = declEdits;
        }

        declEdits.Add(parameter);

        // Apply all edits
        foreach (var entry in documentEdits)
        {
            var doc = solution.GetDocument(entry.Key);
            if (doc is null)
                continue;

            var docRoot = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (docRoot is null)
                continue;

            var newRoot = docRoot.RemoveNodes(entry.Value, SyntaxRemoveOptions.KeepNoTrivia);
            if (newRoot is not null)
            {
                solution = solution.WithDocumentSyntaxRoot(entry.Key, newRoot);
            }
        }

        return solution;
    }

    private static void CollectArgumentToRemove(
        Dictionary<DocumentId, List<SyntaxNode>> edits,
        ReferenceLocation location,
        int parameterIndex
    )
    {
        var node = location.Location.SourceTree?.GetRoot().FindNode(location.Location.SourceSpan);

        if (node is null)
            return;

        BaseArgumentListSyntax? argumentList = null;

        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not null)
        {
            argumentList = invocation.ArgumentList;
        }
        else
        {
            var objectCreation = node.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
            if (objectCreation is not null)
            {
                argumentList = objectCreation.ArgumentList;
            }
            else
            {
                var implicitCreation = node.FirstAncestorOrSelf<ImplicitObjectCreationExpressionSyntax>();
                if (implicitCreation is not null)
                {
                    argumentList = implicitCreation.ArgumentList;
                }
            }
        }

        if (argumentList is null || parameterIndex >= argumentList.Arguments.Count)
            return;

        AddEdit(edits, location.Document.Id, argumentList.Arguments[parameterIndex]);
    }

    private static void AddEdit(Dictionary<DocumentId, List<SyntaxNode>> edits, DocumentId docId, SyntaxNode node)
    {
        if (!edits.TryGetValue(docId, out var list))
        {
            list = [];
            edits[docId] = list;
        }

        list.Add(node);
    }
}
