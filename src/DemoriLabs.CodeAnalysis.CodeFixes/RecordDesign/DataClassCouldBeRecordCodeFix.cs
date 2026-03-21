using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

namespace DemoriLabs.CodeAnalysis.CodeFixes.RecordDesign;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class DataClassCouldBeRecordCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.DataClassCouldBeRecord];

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

        var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to record",
                ct => ConvertToRecordAsync(context.Document, classDecl, ct),
                equivalenceKey: "ConvertToRecord"
            ),
            diagnostic
        );
    }

    private static async Task<Solution> ConvertToRecordAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document.Project.Solution;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null)
            return document.Project.Solution;

        var typeSymbol = semanticModel.GetDeclaredSymbol(classDecl, ct);
        if (typeSymbol is null)
            return document.Project.Solution;

        // Build constructor-to-property assignment map
        var constructor = FindSingleConstructor(classDecl);
        var ctorParamToProperty = new Dictionary<string, ConstructorAssignment>();

        if (constructor is not null)
        {
            BuildConstructorAssignmentMap(constructor, semanticModel, ctorParamToProperty, ct);
        }

        // Find and annotate same-document call sites before modifying the tree
        var callSiteAnnotation = new SyntaxAnnotation("DL1004_CallSite");
        var otherDocCallSites = new Dictionary<DocumentId, List<SyntaxNode>>();

        if (constructor is not null && ctorParamToProperty.Count > 0)
        {
            var ctorSymbol = semanticModel.GetDeclaredSymbol(constructor, ct);
            if (ctorSymbol is not null)
            {
                var references = await SymbolFinder
                    .FindReferencesAsync(ctorSymbol, document.Project.Solution, ct)
                    .ConfigureAwait(false);

                var sameDocCreations = new List<ObjectCreationExpressionSyntax>();

                foreach (var reference in references)
                {
                    foreach (var location in reference.Locations)
                    {
                        if (location.Document.Id == document.Id)
                        {
                            var callNode = root.FindNode(location.Location.SourceSpan);
                            var creation = callNode.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
                            if (creation?.ArgumentList is { Arguments.Count: > 0 })
                                sameDocCreations.Add(creation);
                        }
                        else
                        {
                            if (!otherDocCallSites.TryGetValue(location.Document.Id, out var docList))
                            {
                                docList = [];
                                otherDocCallSites[location.Document.Id] = docList;
                            }

                            var sourceRoot = await location.Location.SourceTree!.GetRootAsync(ct).ConfigureAwait(false);
                            docList.Add(sourceRoot.FindNode(location.Location.SourceSpan));
                        }
                    }
                }

                if (sameDocCreations.Count > 0)
                {
                    root = root.ReplaceNodes(
                        sameDocCreations,
                        (_, rewritten) => rewritten.WithAdditionalAnnotations(callSiteAnnotation)
                    );
                    classDecl = root.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .First(c => c.Identifier.Text == classDecl.Identifier.Text);
                    constructor = FindSingleConstructor(classDecl);
                }
            }
        }

        // Build property name list from constructor parameter order (for call site rewriting)
        var parameterNames = new List<string>();
        if (constructor is not null)
        {
            foreach (var param in constructor.ParameterList.Parameters)
            {
                if (ctorParamToProperty.TryGetValue(param.Identifier.Text, out var assignment))
                {
                    parameterNames.Add(assignment.PropertyName);
                }
            }
        }

        // Detect properties initialised from primary constructor parameters
        var primaryCtorParamNames = new HashSet<string>();
        if (classDecl.ParameterList is not null)
        {
            foreach (var param in classDecl.ParameterList.Parameters)
            {
                primaryCtorParamNames.Add(param.Identifier.Text);
            }
        }

        // Convert the class to a record
        var newMembers = new List<MemberDeclarationSyntax>();
        var assignedPropertyNames = new HashSet<string>(ctorParamToProperty.Values.Select(static a => a.PropertyName));

        SyntaxTriviaList? pendingTrivia = null;

        foreach (var member in classDecl.Members)
        {
            if (member is ConstructorDeclarationSyntax || IsMemberRemovedByRecordConversion(member))
            {
                // Preserve leading trivia (blank lines) from removed members
                var trivia = member.GetLeadingTrivia();
                if (trivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
                    pendingTrivia = trivia;
                continue;
            }

            MemberDeclarationSyntax kept;
            if (member is PropertyDeclarationSyntax property)
            {
                if (assignedPropertyNames.Contains(property.Identifier.Text))
                {
                    kept = ConvertConstructorAssignedProperty(property, ctorParamToProperty);
                }
                else if (IsInitialisedFromPrimaryConstructor(property, primaryCtorParamNames))
                {
                    kept = ConvertPrimaryCtorInitialisedProperty(property);
                }
                else
                {
                    kept = ConvertPropertyToImmutable(property);
                }
            }
            else
            {
                kept = member;
            }

            if (
                pendingTrivia is not null
                && kept.GetLeadingTrivia().Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia)) is false
            )
            {
                kept = kept.WithLeadingTrivia(pendingTrivia.Value);
            }

            pendingTrivia = null;
            newMembers.Add(kept);
        }

        // Always seal the record
        var modifiers = classDecl.Modifiers;
        if (modifiers.Any(SyntaxKind.SealedKeyword) is false)
        {
            var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            var insertIndex = modifiers.IndexOf(SyntaxKind.PublicKeyword);
            modifiers =
                insertIndex >= 0 ? modifiers.Insert(insertIndex + 1, sealedToken) : modifiers.Insert(0, sealedToken);
        }

        var baseList = classDecl.BaseList;

        var recordKeyword = SyntaxFactory.Token(SyntaxKind.RecordKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        var recordDecl = SyntaxFactory
            .RecordDeclaration(recordKeyword, classDecl.Identifier)
            .WithModifiers(modifiers)
            .WithTypeParameterList(classDecl.TypeParameterList)
            .WithBaseList(baseList)
            .WithConstraintClauses(classDecl.ConstraintClauses)
            .WithOpenBraceToken(classDecl.OpenBraceToken)
            .WithCloseBraceToken(classDecl.CloseBraceToken)
            .WithSemicolonToken(classDecl.SemicolonToken)
            .WithAttributeLists(classDecl.AttributeLists)
            .WithMembers(SyntaxFactory.List(newMembers))
            .WithLeadingTrivia(classDecl.GetLeadingTrivia())
            .WithTrailingTrivia(classDecl.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(classDecl, recordDecl);

        // Rewrite same-document call sites (found by annotation)
        var annotatedCallSites = newRoot
            .GetAnnotatedNodes(callSiteAnnotation)
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(c => c.ArgumentList is { Arguments.Count: > 0 })
            .ToList();

        if (annotatedCallSites.Count > 0)
        {
            newRoot = newRoot.ReplaceNodes(
                annotatedCallSites,
                (original, _) =>
                    RewriteCallSite(original, parameterNames).WithAdditionalAnnotations(Formatter.Annotation)
            );
        }

        var currentSolution = document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newRoot);

        // Rewrite call sites in other documents
        foreach (var kvp in otherDocCallSites)
        {
            var targetDoc = currentSolution.GetDocument(kvp.Key);
            if (targetDoc is null)
                continue;

            var targetRoot = await targetDoc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (targetRoot is null)
                continue;

            var creationsToRewrite = kvp
                .Value.Select(n => n.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>())
                .Where(c => c?.ArgumentList is { Arguments.Count: > 0 })
                .Cast<ObjectCreationExpressionSyntax>()
                .ToList();

            if (creationsToRewrite.Count > 0)
            {
                var rewrittenRoot = targetRoot.ReplaceNodes(
                    creationsToRewrite,
                    (original, _) =>
                        RewriteCallSite(original, parameterNames).WithAdditionalAnnotations(Formatter.Annotation)
                );
                currentSolution = currentSolution.WithDocumentSyntaxRoot(kvp.Key, rewrittenRoot);
            }
        }

        return currentSolution;
    }

    private static ConstructorDeclarationSyntax? FindSingleConstructor(ClassDeclarationSyntax classDecl)
    {
        ConstructorDeclarationSyntax? found = null;

        foreach (var member in classDecl.Members)
        {
            if (member is not ConstructorDeclarationSyntax ctor)
                continue;

            if (found is not null)
                return null; // Multiple constructors — don't handle

            found = ctor;
        }

        return found;
    }

    private static void BuildConstructorAssignmentMap(
        ConstructorDeclarationSyntax constructor,
        SemanticModel semanticModel,
        Dictionary<string, ConstructorAssignment> map,
        CancellationToken ct
    )
    {
        var assignments = new List<AssignmentExpressionSyntax>();

        if (constructor.Body is not null)
        {
            foreach (var statement in constructor.Body.Statements)
            {
                if (
                    statement is ExpressionStatementSyntax
                    {
                        Expression: AssignmentExpressionSyntax
                        {
                            RawKind: (int)SyntaxKind.SimpleAssignmentExpression
                        } assignment
                    }
                )
                {
                    assignments.Add(assignment);
                }
            }
        }
        else if (
            constructor.ExpressionBody?.Expression is AssignmentExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression
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
                continue;

            if (semanticModel.GetSymbolInfo(leftTarget, ct).Symbol is not IPropertySymbol propertySymbol)
                continue;

            var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right, ct).Symbol;
            if (rightSymbol is not IParameterSymbol paramSymbol)
                continue;

            var paramSyntax = constructor.ParameterList.Parameters.FirstOrDefault(p =>
                p.Identifier.Text == paramSymbol.Name
            );

            map[paramSymbol.Name] = new ConstructorAssignment(propertySymbol.Name, paramSyntax?.Default?.Value);
        }
    }

    private static PropertyDeclarationSyntax ConvertConstructorAssignedProperty(
        PropertyDeclarationSyntax property,
        Dictionary<string, ConstructorAssignment> ctorParamToProperty
    )
    {
        var assignment = ctorParamToProperty.Values.FirstOrDefault(a => a.PropertyName == property.Identifier.Text);
        var newProperty = EnsureGetInit(property);

        if (assignment?.DefaultValue is not null)
        {
            // Has default value — not required, add initializer
            newProperty = newProperty
                .WithInitializer(SyntaxFactory.EqualsValueClause(assignment.DefaultValue.WithoutTrivia()))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }
        else if (property.Modifiers.Any(m => m.IsKind(SyntaxKind.RequiredKeyword)) is false)
        {
            // No default — add required
            var requiredToken = SyntaxFactory.Token(SyntaxKind.RequiredKeyword).WithTrailingTrivia(SyntaxFactory.Space);

            var publicIndex = newProperty.Modifiers.IndexOf(SyntaxKind.PublicKeyword);
            newProperty =
                publicIndex >= 0
                    ? newProperty.WithModifiers(newProperty.Modifiers.Insert(publicIndex + 1, requiredToken))
                    : newProperty.AddModifiers(requiredToken);
        }

        return newProperty;
    }

    private static PropertyDeclarationSyntax EnsureGetInit(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is null)
            return property;

        var newAccessors = new List<AccessorDeclarationSyntax>();
        var needsInit = true;

        foreach (var accessor in property.AccessorList.Accessors)
        {
            if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                newAccessors.Add(
                    SyntaxFactory
                        .AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                        .WithLeadingTrivia(accessor.GetLeadingTrivia())
                        .WithTrailingTrivia(accessor.GetTrailingTrivia())
                );
                needsInit = false;
            }
            else if (accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
            {
                newAccessors.Add(accessor);
                needsInit = false;
            }
            else
            {
                newAccessors.Add(accessor);
            }
        }

        // Get-only property — add init accessor
        if (needsInit)
        {
            var lastAccessor = newAccessors[newAccessors.Count - 1];
            newAccessors.Add(
                SyntaxFactory
                    .AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    .WithLeadingTrivia(lastAccessor.GetLeadingTrivia())
                    .WithTrailingTrivia(lastAccessor.GetTrailingTrivia())
            );
        }

        return property.WithAccessorList(property.AccessorList.WithAccessors(SyntaxFactory.List(newAccessors)));
    }

    private static PropertyDeclarationSyntax ConvertPropertyToImmutable(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is null)
            return property;

        var hasDefault = property.Initializer is not null;
        var hasSetter = property.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        var hasInit = property.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));

        if (!hasSetter && !hasInit)
            return property;

        var newProperty = property;

        if (hasSetter)
        {
            var newAccessors = new List<AccessorDeclarationSyntax>();

            foreach (var accessor in property.AccessorList.Accessors)
            {
                if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
                {
                    newAccessors.Add(
                        SyntaxFactory
                            .AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                            .WithLeadingTrivia(accessor.GetLeadingTrivia())
                            .WithTrailingTrivia(accessor.GetTrailingTrivia())
                    );
                }
                else
                {
                    newAccessors.Add(accessor);
                }
            }

            newProperty = newProperty.WithAccessorList(
                property.AccessorList.WithAccessors(SyntaxFactory.List(newAccessors))
            );
        }

        if (hasDefault || property.Modifiers.Any(m => m.IsKind(SyntaxKind.RequiredKeyword)))
            return newProperty;

        var requiredToken = SyntaxFactory.Token(SyntaxKind.RequiredKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        var publicIndex = newProperty.Modifiers.IndexOf(SyntaxKind.PublicKeyword);
        newProperty =
            publicIndex >= 0
                ? newProperty.WithModifiers(newProperty.Modifiers.Insert(publicIndex + 1, requiredToken))
                : newProperty.AddModifiers(requiredToken);

        return newProperty;
    }

    private static ExpressionSyntax RewriteCallSite(ObjectCreationExpressionSyntax creation, List<string> propertyNames)
    {
        if (creation.ArgumentList is null)
            return creation;

        var args = creation.ArgumentList.Arguments;
        var assignments = new List<ExpressionSyntax>();

        for (var i = 0; i < args.Count && i < propertyNames.Count; i++)
        {
            assignments.Add(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(propertyNames[i]),
                    args[i].Expression.WithoutTrivia()
                )
            );
        }

        var initializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.ObjectInitializerExpression,
            SyntaxFactory.SeparatedList(assignments)
        );

        return creation
            .WithArgumentList(null)
            .WithInitializer(initializer)
            .NormalizeWhitespace("    ", "\n")
            .WithLeadingTrivia(creation.GetLeadingTrivia())
            .WithTrailingTrivia(creation.GetTrailingTrivia());
    }

    private static bool IsInitialisedFromPrimaryConstructor(
        PropertyDeclarationSyntax property,
        HashSet<string> primaryCtorParamNames
    )
    {
        return primaryCtorParamNames.Count > 0
            && property.Initializer?.Value is IdentifierNameSyntax identifier
            && primaryCtorParamNames.Contains(identifier.Identifier.Text);
    }

    private static PropertyDeclarationSyntax ConvertPrimaryCtorInitialisedProperty(PropertyDeclarationSyntax property)
    {
        var newProperty = EnsureGetInit(property).WithInitializer(null).WithSemicolonToken(default);

        if (property.Modifiers.Any(m => m.IsKind(SyntaxKind.RequiredKeyword)) is false)
        {
            var requiredToken = SyntaxFactory.Token(SyntaxKind.RequiredKeyword).WithTrailingTrivia(SyntaxFactory.Space);

            var publicIndex = newProperty.Modifiers.IndexOf(SyntaxKind.PublicKeyword);
            newProperty =
                publicIndex >= 0
                    ? newProperty.WithModifiers(newProperty.Modifiers.Insert(publicIndex + 1, requiredToken))
                    : newProperty.AddModifiers(requiredToken);
        }

        return newProperty;
    }

    private static bool IsMemberRemovedByRecordConversion(MemberDeclarationSyntax member)
    {
        return member switch
        {
            // override bool Equals(object?) — record synthesises this
            MethodDeclarationSyntax { Identifier.Text: "Equals" } method => method.ParameterList.Parameters.Count == 1
                && method.ParameterList.Parameters[0].Type
                    is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword }
                        or NullableTypeSyntax
                        {
                            ElementType: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword }
                        },
            // operator == and operator !=
            OperatorDeclarationSyntax op => op.OperatorToken.Kind()
                is SyntaxKind.EqualsEqualsToken
                    or SyntaxKind.ExclamationEqualsToken,
            // PrintMembers — compiler-reserved
            MethodDeclarationSyntax { Identifier.Text: "PrintMembers" } => true,
            _ => false,
        };
    }

    private sealed class ConstructorAssignment(string propertyName, ExpressionSyntax? defaultValue)
    {
        public string PropertyName { get; } = propertyName;
        public ExpressionSyntax? DefaultValue { get; } = defaultValue;
    }
}
