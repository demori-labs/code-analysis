using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.Diagnostics.CodeFixes;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class MultipleEnumerationCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.PossibleMultipleEnumeration];

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

        if (node is not IdentifierNameSyntax identifier)
        {
            return;
        }

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var symbol = semanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
        if (symbol is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Materialize '{symbol.Name}' with ToList()",
                ct => MaterializeEnumerableAsync(context.Document, semanticModel, symbol, ct),
                equivalenceKey: $"MaterializeEnumerable_{symbol.Name}"
            ),
            diagnostic
        );
    }

    private static async Task<Document> MaterializeEnumerableAsync(
        Document document,
        SemanticModel semanticModel,
        ISymbol symbol,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var declaringSyntax = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (declaringSyntax is null)
        {
            return document;
        }

        var syntaxNode = await declaringSyntax.GetSyntaxAsync(ct).ConfigureAwait(false);
        var containingMember = syntaxNode
            .Ancestors()
            .FirstOrDefault(n =>
                n is MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax
            );

        var body = containingMember switch
        {
            MethodDeclarationSyntax m => m.Body,
            ConstructorDeclarationSyntax c => c.Body,
            LocalFunctionStatementSyntax l => l.Body,
            _ => null,
        };

        if (body is null || body.Statements.Count == 0)
        {
            return document;
        }

        var newVarName = symbol.Name + "List";

        var references = body.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(id, ct).Symbol, symbol))
            .ToList();

        var newBody = body.ReplaceNodes(
            references,
            (original, _) => SyntaxFactory.IdentifierName(newVarName).WithTriviaFrom(original)
        );

        var materializationStatement = SyntaxFactory
            .LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName(
                        SyntaxFactory.Identifier(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.VarKeyword,
                            "var",
                            "var",
                            SyntaxFactory.TriviaList(SyntaxFactory.Space)
                        )
                    ),
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory
                            .VariableDeclarator(SyntaxFactory.Identifier(newVarName))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.InvocationExpression(
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName(symbol.Name),
                                            SyntaxFactory.IdentifierName("ToList")
                                        )
                                    )
                                )
                            )
                    )
                )
            )
            .WithLeadingTrivia(body.Statements[0].GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        newBody = newBody.WithStatements(newBody.Statements.Insert(0, materializationStatement));

        var newRoot = root.ReplaceNode(body, newBody);

        newRoot = newRoot.EnsureUsingDirective(semanticModel, "System.Linq");

        return document.WithSyntaxRoot(newRoot);
    }
}
