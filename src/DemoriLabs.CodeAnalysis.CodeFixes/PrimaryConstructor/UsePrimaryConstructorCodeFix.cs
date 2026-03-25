using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;

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

    private static async Task<Solution> UsePrimaryConstructorAsync(
        Document document,
        ConstructorDeclarationSyntax constructorDecl,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document.Project.Solution;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null || constructorDecl.Parent is not TypeDeclarationSyntax)
            return document.Project.Solution;

        var memberToParamMap = BuildMemberToParameterMap(constructorDecl, semanticModel, ct);
        if (memberToParamMap.Count is 0 && constructorDecl.ParameterList.Parameters.Count is 0)
            return document.Project.Solution;

        var currentSolution = document.Project.Solution;

        foreach (var kvp in memberToParamMap)
        {
            // Only rename private readonly fields (they get removed and replaced by the parameter)
            if (
                kvp.Key is IFieldSymbol { DeclaredAccessibility: Accessibility.Private, IsReadOnly: true } fieldSymbol
                && string.Equals(fieldSymbol.Name, kvp.Value, StringComparison.Ordinal) is false
            )
            {
                currentSolution = await Renamer
                    .RenameSymbolAsync(currentSolution, fieldSymbol, new SymbolRenameOptions(), kvp.Value, ct)
                    .ConfigureAwait(false);
            }
        }

        var currentDocument = currentSolution.GetDocument(document.Id)!;
        var currentRoot = await currentDocument.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var currentSemanticModel = await currentDocument.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (currentRoot is null || currentSemanticModel is null)
            return currentSolution;

        var currentConstructor = currentRoot
            .DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == constructorDecl.Identifier.Text);

        if (currentConstructor is null || currentConstructor.Parent is not TypeDeclarationSyntax currentTypeDecl)
            return currentSolution;

        var currentMemberMap = BuildMemberToParameterMap(currentConstructor, currentSemanticModel, ct);

        var fieldSymbolToParamName = new Dictionary<IFieldSymbol, string>(SymbolEqualityComparer.Default);
        var nonPrivateFieldToParam = new Dictionary<string, string>();
        var propertyToParam = new Dictionary<string, string>();
        var fieldsToRemove = new HashSet<string>();

        foreach (var kvp in currentMemberMap)
        {
            switch (kvp.Key)
            {
                case IFieldSymbol { DeclaredAccessibility: Accessibility.Private, IsReadOnly: true } readonlyField:
                    fieldSymbolToParamName[readonlyField] = kvp.Value;
                    fieldsToRemove.Add(readonlyField.Name);
                    break;
                case IFieldSymbol field:
                    nonPrivateFieldToParam[field.Name] = kvp.Value;
                    break;
                case IPropertySymbol property:
                    propertyToParam[property.Name] = kvp.Value;
                    break;
            }
        }

        var isReadOnlyStruct =
            currentTypeDecl is StructDeclarationSyntax && currentTypeDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        var primaryParams = BuildPrimaryConstructorParameters(currentConstructor, isReadOnlyStruct);
        var parameterList = FormatParameterList(
            primaryParams,
            currentTypeDecl,
            currentDocument,
            currentRoot.SyntaxTree
        );

        var rewriter = new PrimaryConstructorRewriter(fieldSymbolToParamName, currentSemanticModel, ct);
        var rewrittenType = (TypeDeclarationSyntax)rewriter.Visit(currentTypeDecl);

        var membersWithoutConstructor = new SyntaxList<MemberDeclarationSyntax>();
        foreach (var member in rewrittenType.Members)
        {
            // Only remove the main constructor, keep secondary ones that chain via : this(...)
            if (
                member is ConstructorDeclarationSyntax ctor
                && ctor.Identifier.Text == constructorDecl.Identifier.Text
                && ctor.ParameterList.Parameters.Count == constructorDecl.ParameterList.Parameters.Count
                && ctor.Initializer?.ThisOrBaseKeyword.Kind() is not SyntaxKind.ThisKeyword
            )
            {
                continue;
            }

            membersWithoutConstructor = membersWithoutConstructor.Add(member);
        }

        var finalMembers = new SyntaxList<MemberDeclarationSyntax>();
        foreach (var member in membersWithoutConstructor)
        {
            if (member is FieldDeclarationSyntax fieldDecl)
            {
                var newVariables = new List<VariableDeclaratorSyntax>();
                var skipEntireField = true;

                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    if (fieldsToRemove.Contains(variable.Identifier.Text))
                        continue;

                    if (nonPrivateFieldToParam.TryGetValue(variable.Identifier.Text, out var paramName))
                    {
                        newVariables.Add(
                            variable.WithInitializer(
                                SyntaxFactory.EqualsValueClause(SyntaxFactory.IdentifierName(paramName))
                            )
                        );
                        skipEntireField = false;
                    }
                    else
                    {
                        newVariables.Add(variable);
                        skipEntireField = false;
                    }
                }

                if (skipEntireField)
                    continue;

                var newDeclaration = fieldDecl.Declaration.WithVariables(SyntaxFactory.SeparatedList(newVariables));
                finalMembers = finalMembers.Add(fieldDecl.WithDeclaration(newDeclaration));
                continue;
            }

            if (
                member is PropertyDeclarationSyntax propDecl
                && propertyToParam.TryGetValue(propDecl.Identifier.Text, out var propParamName)
            )
            {
                finalMembers = finalMembers.Add(
                    propDecl
                        .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.IdentifierName(propParamName)))
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                );
                continue;
            }

            finalMembers = finalMembers.Add(member);
        }

        if (finalMembers.Count > 0)
        {
            var firstMember = finalMembers[0];
            var cleanedTrivia = NormalizeLeadingTrivia(
                firstMember.GetLeadingTrivia(),
                currentDocument,
                currentRoot.SyntaxTree
            );
            finalMembers = finalMembers.Replace(firstMember, firstMember.WithLeadingTrivia(cleanedTrivia));
        }

        var identifier = rewrittenType.Identifier;
        var openBrace = rewrittenType.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
        var newTypeDecl = rewrittenType
            .WithIdentifier(identifier.WithTrailingTrivia())
            .WithOpenBraceToken(openBrace)
            .WithMembers(finalMembers)
            .WithParameterList(parameterList);
        newTypeDecl = HandleBaseInitializer(newTypeDecl, currentConstructor);

        var newRoot = currentRoot.ReplaceNode(
            currentTypeDecl,
            newTypeDecl.WithAdditionalAnnotations(Formatter.Annotation)
        );
        if (isReadOnlyStruct is false)
        {
            newRoot = newRoot.EnsureUsingDirective(currentSemanticModel, "DemoriLabs.CodeAnalysis.Attributes");
        }

        return currentSolution.WithDocumentSyntaxRoot(document.Id, newRoot);
    }

    private static List<KeyValuePair<ISymbol, string>> BuildMemberToParameterMap(
        ConstructorDeclarationSyntax constructorDecl,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var result = new List<KeyValuePair<ISymbol, string>>();
        var assignments = new List<AssignmentExpressionSyntax>();

        if (constructorDecl.Body is not null)
        {
            foreach (var statement in constructorDecl.Body.Statements)
            {
                if (
                    statement is ExpressionStatementSyntax
                    {
                        Expression: AssignmentExpressionSyntax
                        {
                            RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                        } assignment,
                    }
                )
                {
                    assignments.Add(assignment);
                }
            }
        }
        else if (
            constructorDecl.ExpressionBody?.Expression is AssignmentExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
            } exprAssignment
        )
        {
            assignments.Add(exprAssignment);
        }

        foreach (var assignment in assignments)
        {
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

            var memberSymbol = semanticModel.GetSymbolInfo(leftTarget, ct).Symbol;
            if (memberSymbol is not (IFieldSymbol or IPropertySymbol))
            {
                continue;
            }

            var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right, ct).Symbol;
            if (rightSymbol is IParameterSymbol paramSymbol)
            {
                result.Add(new KeyValuePair<ISymbol, string>(memberSymbol, paramSymbol.Name));
            }
        }

        return result;
    }

    private static List<ParameterSyntax> BuildPrimaryConstructorParameters(
        ConstructorDeclarationSyntax constructorDecl,
        bool isReadOnlyStruct
    )
    {
        // Readonly struct parameters are already readonly — don't add [ReadOnly]
        if (isReadOnlyStruct)
        {
            return
            [
                .. constructorDecl.ParameterList.Parameters.Select(static param =>
                    SyntaxFactory
                        .Parameter(param.Identifier)
                        .WithType(param.Type)
                        .WithAttributeLists(param.AttributeLists)
                ),
            ];
        }

        var readOnlyAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("ReadOnly"));
        var readOnlyAttrList = SyntaxFactory
            .AttributeList(SyntaxFactory.SingletonSeparatedList(readOnlyAttribute))
            .WithTrailingTrivia(SyntaxFactory.Space);

        return
        [
            .. constructorDecl.ParameterList.Parameters.Select(param =>
            {
                var attrs = param.AttributeLists.Add(readOnlyAttrList);
                return SyntaxFactory.Parameter(param.Identifier).WithType(param.Type).WithAttributeLists(attrs);
            }),
        ];
    }

    private static ParameterListSyntax FormatParameterList(
        List<ParameterSyntax> parameters,
        TypeDeclarationSyntax typeDecl,
        Document document,
        SyntaxTree syntaxTree
    )
    {
        if (parameters.Count is 0)
        {
            return SyntaxFactory.ParameterList().WithTrailingTrivia(SyntaxFactory.Space);
        }

        // Detect the type declaration's indentation
        var typeIndent =
            typeDecl
                .GetLeadingTrivia()
                .Where(static t => t.IsKind(SyntaxKind.WhitespaceTrivia))
                .Select(static t => t.ToString())
                .LastOrDefault()
            ?? "";

        var indentUnit = IndentationResolver.GetIndentUnit(document, syntaxTree);
        var paramIndent = typeIndent + indentUnit;
        var newLine = SyntaxFactory.EndOfLine("\n");
        var paramIndentTrivia = SyntaxFactory.Whitespace(paramIndent);
        var typeIndentTrivia = SyntaxFactory.Whitespace(typeIndent);

        // Format each parameter with leading newline + indentation
        var formattedParams = new List<ParameterSyntax>(parameters.Count);
        foreach (var param in parameters)
        {
            formattedParams.Add(param.WithLeadingTrivia(newLine, paramIndentTrivia));
        }

        // Build separated list with commas (no trailing comma)
        var separatedList = SyntaxFactory.SeparatedList(formattedParams);

        return SyntaxFactory
            .ParameterList(separatedList)
            .WithCloseParenToken(
                SyntaxFactory.Token(SyntaxKind.CloseParenToken).WithLeadingTrivia(newLine, typeIndentTrivia)
            );
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
