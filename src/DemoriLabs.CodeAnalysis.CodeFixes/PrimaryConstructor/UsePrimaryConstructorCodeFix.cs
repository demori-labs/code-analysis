using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.PrimaryConstructor;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class UsePrimaryConstructorCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UsePrimaryConstructor];

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

        var constructorDecl = node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (constructorDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use primary constructor",
                ct => UsePrimaryConstructorAsync(context.Document, constructorDecl, ct),
                equivalenceKey: nameof(UsePrimaryConstructorCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> UsePrimaryConstructorAsync(
        Document document,
        ConstructorDeclarationSyntax constructorDecl,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null || constructorDecl.Parent is not TypeDeclarationSyntax typeDecl)
        {
            return document;
        }

        var fieldToParamMap = BuildFieldToParameterMap(constructorDecl, semanticModel, ct);
        if (fieldToParamMap.Count is 0 && constructorDecl.ParameterList.Parameters.Count is 0)
        {
            return document;
        }

        // Build the field symbol to new parameter name map
        var fieldSymbolToParamName = new Dictionary<IFieldSymbol, string>(SymbolEqualityComparer.Default);
        foreach (var kvp in fieldToParamMap)
        {
            fieldSymbolToParamName[kvp.Key] = kvp.Value;
        }

        // Build primary constructor parameters with [ReadOnly]
        var primaryParams = BuildPrimaryConstructorParameters(constructorDecl);

        // Build parameter list
        var parameterList = SyntaxFactory
            .ParameterList(SyntaxFactory.SeparatedList(primaryParams))
            .WithTrailingTrivia(SyntaxFactory.Space);

        // Rewrite: replace field references, remove constructor, remove fields
        var rewriter = new PrimaryConstructorRewriter(fieldSymbolToParamName, semanticModel, ct);
        var rewrittenType = (TypeDeclarationSyntax)rewriter.Visit(typeDecl);

        // Remove the constructor from members
        var membersWithoutConstructor = new SyntaxList<MemberDeclarationSyntax>();
        foreach (var member in rewrittenType.Members)
        {
            if (member is ConstructorDeclarationSyntax)
            {
                continue;
            }

            membersWithoutConstructor = membersWithoutConstructor.Add(member);
        }

        // Remove backing fields
        var fieldsToRemove = new HashSet<string>(fieldToParamMap.Select(kvp => kvp.Key.Name));
        var finalMembers = new SyntaxList<MemberDeclarationSyntax>();
        foreach (var member in membersWithoutConstructor)
        {
            if (member is FieldDeclarationSyntax fieldDecl)
            {
                var remainingVariables = fieldDecl
                    .Declaration.Variables.Where(v => fieldsToRemove.Contains(v.Identifier.Text) is false)
                    .ToList();

                if (remainingVariables.Count is 0)
                {
                    continue;
                }

                if (remainingVariables.Count < fieldDecl.Declaration.Variables.Count)
                {
                    var newDeclaration = fieldDecl.Declaration.WithVariables(
                        SyntaxFactory.SeparatedList(remainingVariables)
                    );
                    finalMembers = finalMembers.Add(fieldDecl.WithDeclaration(newDeclaration));
                    continue;
                }
            }

            finalMembers = finalMembers.Add(member);
        }

        // Clean up leading trivia on first remaining member to avoid double blank lines
        if (finalMembers.Count > 0)
        {
            var firstMember = finalMembers[0];
            var cleanedTrivia = NormalizeLeadingTrivia(firstMember.GetLeadingTrivia(), document, root.SyntaxTree);
            finalMembers = finalMembers.Replace(firstMember, firstMember.WithLeadingTrivia(cleanedTrivia));
        }

        // Move trailing trivia from identifier to after parameter list
        var identifier = rewrittenType.Identifier;
        // Ensure open brace only has a single trailing newline
        var openBrace = rewrittenType.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
        var newTypeDecl = rewrittenType
            .WithIdentifier(identifier.WithTrailingTrivia())
            .WithOpenBraceToken(openBrace)
            .WithMembers(finalMembers)
            .WithParameterList(parameterList);
        newTypeDecl = HandleBaseInitializer(newTypeDecl, constructorDecl);

        var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);
        newRoot = newRoot.EnsureUsingDirective(semanticModel, "DemoriLabs.CodeAnalysis.Attributes");

        return document.WithSyntaxRoot(newRoot);
    }

    private static List<KeyValuePair<IFieldSymbol, string>> BuildFieldToParameterMap(
        ConstructorDeclarationSyntax constructorDecl,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var result = new List<KeyValuePair<IFieldSymbol, string>>();

        if (constructorDecl.Body is null)
        {
            return result;
        }

        foreach (var statement in constructorDecl.Body.Statements)
        {
            if (
                statement
                is not ExpressionStatementSyntax
                {
                    Expression: AssignmentExpressionSyntax
                    {
                        RawKind: (int)SyntaxKind.SimpleAssignmentExpression
                    } assignment
                }
            )
            {
                continue;
            }

            var leftTarget = assignment.Left switch
            {
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess => memberAccess
                    as ExpressionSyntax,
                IdentifierNameSyntax identifier => identifier,
                _ => null,
            };

            if (leftTarget is null)
            {
                continue;
            }

            if (semanticModel.GetSymbolInfo(leftTarget, ct).Symbol is not IFieldSymbol fieldSymbol)
            {
                continue;
            }

            var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right, ct).Symbol;
            if (rightSymbol is IParameterSymbol paramSymbol)
            {
                result.Add(new KeyValuePair<IFieldSymbol, string>(fieldSymbol, paramSymbol.Name));
            }
        }

        return result;
    }

    private static List<ParameterSyntax> BuildPrimaryConstructorParameters(ConstructorDeclarationSyntax constructorDecl)
    {
        var readOnlyAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("ReadOnly"));
        var attributeList = SyntaxFactory
            .AttributeList(SyntaxFactory.SingletonSeparatedList(readOnlyAttribute))
            .WithTrailingTrivia(SyntaxFactory.Space);

        return
        [
            .. constructorDecl.ParameterList.Parameters.Select(param =>
                SyntaxFactory
                    .Parameter(param.Identifier)
                    .WithType(param.Type)
                    .WithAttributeLists(SyntaxFactory.SingletonList(attributeList))
            ),
        ];
    }

    private static TypeDeclarationSyntax HandleBaseInitializer(
        TypeDeclarationSyntax newTypeDecl,
        ConstructorDeclarationSyntax originalConstructor
    )
    {
        if (
            originalConstructor.Initializer is not { } initializer
            || initializer.ThisOrBaseKeyword.Kind() is not SyntaxKind.BaseKeyword
            || newTypeDecl.BaseList is null
        )
        {
            return newTypeDecl;
        }

        var newBaseTypes = new SeparatedSyntaxList<BaseTypeSyntax>();
        var updated = false;

        foreach (var baseType in newTypeDecl.BaseList.Types)
        {
            if (updated is false && baseType is SimpleBaseTypeSyntax simpleBase)
            {
                var cleanedArgList = initializer.ArgumentList.WithLeadingTrivia().WithTrailingTrivia();
                var primaryConstructorBase = SyntaxFactory
                    .PrimaryConstructorBaseType(simpleBase.Type.WithTrailingTrivia(), cleanedArgList)
                    .WithLeadingTrivia(simpleBase.GetLeadingTrivia())
                    .WithTrailingTrivia(simpleBase.GetTrailingTrivia());

                newBaseTypes = newBaseTypes.Add(primaryConstructorBase);
                updated = true;
            }
            else
            {
                newBaseTypes = newBaseTypes.Add(baseType);
            }
        }

        return newTypeDecl.WithBaseList(newTypeDecl.BaseList.WithTypes(newBaseTypes));
    }

    private static SyntaxTriviaList NormalizeLeadingTrivia(
        SyntaxTriviaList trivia,
        Document document,
        SyntaxTree syntaxTree
    )
    {
        // Keep only indentation — the open brace token already provides the newline
        var lastWhitespace = trivia
            .Where(static t => t.Kind() is SyntaxKind.WhitespaceTrivia)
            .Select(static t => (SyntaxTrivia?)t)
            .LastOrDefault();

        var indentation =
            lastWhitespace ?? SyntaxFactory.Whitespace(IndentationResolver.GetIndentUnit(document, syntaxTree));

        return SyntaxFactory.TriviaList(indentation);
    }

    private sealed class PrimaryConstructorRewriter(
        Dictionary<IFieldSymbol, string> fieldToParamName,
        SemanticModel semanticModel,
        CancellationToken ct
    ) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Expression is not ThisExpressionSyntax)
                return base.VisitMemberAccessExpression(node);

            var symbol = semanticModel.GetSymbolInfo(node, ct).Symbol;
            if (symbol is IFieldSymbol fieldSymbol && fieldToParamName.TryGetValue(fieldSymbol, out var paramName))
            {
                return SyntaxFactory
                    .IdentifierName(paramName)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = semanticModel.GetSymbolInfo(node, ct).Symbol;
            if (symbol is IFieldSymbol fieldSymbol && fieldToParamName.TryGetValue(fieldSymbol, out var paramName))
            {
                return SyntaxFactory
                    .IdentifierName(paramName)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            return base.VisitIdentifierName(node);
        }
    }
}
