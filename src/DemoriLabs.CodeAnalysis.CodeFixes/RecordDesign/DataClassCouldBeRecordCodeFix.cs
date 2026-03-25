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

        var constructor = FindSingleConstructor(classDecl);
        var ctorParamToProperty = new Dictionary<string, ConstructorAssignment>();

        if (constructor is not null)
        {
            BuildConstructorAssignmentMap(constructor, semanticModel, ctorParamToProperty, ct);
        }

        var callSiteAnnotation = new SyntaxAnnotation("DL1004_CallSite");
        var otherDocCallSites = new Dictionary<DocumentId, List<SyntaxNode>>();

        var ctorSymbolToFind = constructor is not null
            ? semanticModel.GetDeclaredSymbol(constructor, ct)
            : FindPrimaryConstructorSymbol(typeSymbol, ct);

        if (ctorSymbolToFind is not null)
        {
            var (sameDocCreations, otherDocRefs) = await CollectCallSiteReferencesAsync(
                    ctorSymbolToFind,
                    document,
                    root,
                    ct
                )
                .ConfigureAwait(false);

            otherDocCallSites = otherDocRefs;

            (root, classDecl, constructor) = AnnotateSameDocumentCallSites(
                root,
                classDecl,
                sameDocCreations,
                callSiteAnnotation
            );
        }

        var parameterNames = constructor is not null
            ? BuildParameterNamesFromConstructor(constructor, ctorParamToProperty)
            : BuildParameterNamesFromPrimaryConstructor(classDecl);

        var primaryCtorParamNames = BuildPrimaryCtorParamNameSet(classDecl);
        var assignedPropertyNames = new HashSet<string>(ctorParamToProperty.Values.Select(static a => a.PropertyName));

        var newMembers = ConvertClassMembersToRecordMembers(
            classDecl,
            assignedPropertyNames,
            ctorParamToProperty,
            primaryCtorParamNames
        );

        var recordDecl = BuildRecordDeclaration(classDecl, newMembers);
        var newRoot = root.ReplaceNode(classDecl, recordDecl);
        newRoot = RewriteSameDocumentCallSites(newRoot, callSiteAnnotation, parameterNames);

        var currentSolution = document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newRoot);

        return await RewriteOtherDocumentCallSitesAsync(currentSolution, otherDocCallSites, parameterNames, ct)
            .ConfigureAwait(false);
    }

    private static async Task<(
        List<ObjectCreationExpressionSyntax> SameDocCreations,
        Dictionary<DocumentId, List<SyntaxNode>> OtherDocCallSites
    )> CollectCallSiteReferencesAsync(
        IMethodSymbol ctorSymbol,
        Document document,
        SyntaxNode root,
        CancellationToken ct
    )
    {
        var sameDocCreations = new List<ObjectCreationExpressionSyntax>();
        var otherDocCallSites = new Dictionary<DocumentId, List<SyntaxNode>>();

        var references = await SymbolFinder
            .FindReferencesAsync(ctorSymbol, document.Project.Solution, ct)
            .ConfigureAwait(false);

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
                    if (otherDocCallSites.TryGetValue(location.Document.Id, out var docList) is false)
                    {
                        docList = [];
                        otherDocCallSites[location.Document.Id] = docList;
                    }

                    var sourceRoot = await location.Location.SourceTree!.GetRootAsync(ct).ConfigureAwait(false);
                    docList.Add(sourceRoot.FindNode(location.Location.SourceSpan));
                }
            }
        }

        return (sameDocCreations, otherDocCallSites);
    }

    private static (
        SyntaxNode Root,
        ClassDeclarationSyntax ClassDecl,
        ConstructorDeclarationSyntax? Constructor
    ) AnnotateSameDocumentCallSites(
        SyntaxNode root,
        ClassDeclarationSyntax classDecl,
        List<ObjectCreationExpressionSyntax> sameDocCreations,
        SyntaxAnnotation callSiteAnnotation
    )
    {
        if (sameDocCreations.Count is 0)
            return (root, classDecl, FindSingleConstructor(classDecl));

        root = root.ReplaceNodes(
            sameDocCreations,
            (_, rewritten) => rewritten.WithAdditionalAnnotations(callSiteAnnotation)
        );

        classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == classDecl.Identifier.Text);

        return (root, classDecl, FindSingleConstructor(classDecl));
    }

    private static List<string> BuildParameterNamesFromConstructor(
        ConstructorDeclarationSyntax constructor,
        Dictionary<string, ConstructorAssignment> ctorParamToProperty
    )
    {
        var parameterNames = new List<string>();

        foreach (var param in constructor.ParameterList.Parameters)
        {
            if (ctorParamToProperty.TryGetValue(param.Identifier.Text, out var assignment))
            {
                parameterNames.Add(assignment.PropertyName);
            }
        }

        return parameterNames;
    }

    private static List<string> BuildParameterNamesFromPrimaryConstructor(ClassDeclarationSyntax classDecl)
    {
        var parameterNames = new List<string>();
        var primaryCtorParamNames = BuildPrimaryCtorParamNameSet(classDecl);

        if (primaryCtorParamNames.Count is 0)
            return parameterNames;

        foreach (var member in classDecl.Members)
        {
            if (
                member is PropertyDeclarationSyntax prop
                && IsInitialisedFromPrimaryConstructor(prop, primaryCtorParamNames)
            )
            {
                parameterNames.Add(prop.Identifier.Text);
            }
        }

        return parameterNames;
    }

    private static HashSet<string> BuildPrimaryCtorParamNameSet(ClassDeclarationSyntax classDecl)
    {
        var names = new HashSet<string>();

        if (classDecl.ParameterList is null)
            return names;

        foreach (var param in classDecl.ParameterList.Parameters)
        {
            names.Add(param.Identifier.Text);
        }

        return names;
    }

    private static List<MemberDeclarationSyntax> ConvertClassMembersToRecordMembers(
        ClassDeclarationSyntax classDecl,
        HashSet<string> assignedPropertyNames,
        Dictionary<string, ConstructorAssignment> ctorParamToProperty,
        HashSet<string> primaryCtorParamNames
    )
    {
        var newMembers = new List<MemberDeclarationSyntax>();
        SyntaxTriviaList? pendingTrivia = null;

        foreach (var member in classDecl.Members)
        {
            if (member is ConstructorDeclarationSyntax || IsMemberRemovedByRecordConversion(member))
            {
                var trivia = member.GetLeadingTrivia();
                if (trivia.Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
                    pendingTrivia = trivia;
                continue;
            }

            var kept = ConvertSingleMember(member, assignedPropertyNames, ctorParamToProperty, primaryCtorParamNames);

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

        return newMembers;
    }

    private static MemberDeclarationSyntax ConvertSingleMember(
        MemberDeclarationSyntax member,
        HashSet<string> assignedPropertyNames,
        Dictionary<string, ConstructorAssignment> ctorParamToProperty,
        HashSet<string> primaryCtorParamNames
    )
    {
        if (member is not PropertyDeclarationSyntax property)
            return member;

        if (assignedPropertyNames.Contains(property.Identifier.Text))
            return ConvertConstructorAssignedProperty(property, ctorParamToProperty);

        if (IsInitialisedFromPrimaryConstructor(property, primaryCtorParamNames))
            return ConvertPrimaryCtorInitialisedProperty(property);

        return ConvertPropertyToImmutable(property);
    }

    private static RecordDeclarationSyntax BuildRecordDeclaration(
        ClassDeclarationSyntax classDecl,
        List<MemberDeclarationSyntax> newMembers
    )
    {
        var modifiers = classDecl.Modifiers;
        if (modifiers.Any(SyntaxKind.SealedKeyword) is false)
        {
            var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            var insertIndex = modifiers.IndexOf(SyntaxKind.PublicKeyword);
            modifiers =
                insertIndex >= 0 ? modifiers.Insert(insertIndex + 1, sealedToken) : modifiers.Insert(0, sealedToken);
        }

        var recordKeyword = SyntaxFactory.Token(SyntaxKind.RecordKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        return SyntaxFactory
            .RecordDeclaration(recordKeyword, classDecl.Identifier)
            .WithModifiers(modifiers)
            .WithTypeParameterList(classDecl.TypeParameterList)
            .WithBaseList(classDecl.BaseList)
            .WithConstraintClauses(classDecl.ConstraintClauses)
            .WithOpenBraceToken(classDecl.OpenBraceToken)
            .WithCloseBraceToken(classDecl.CloseBraceToken)
            .WithSemicolonToken(classDecl.SemicolonToken)
            .WithAttributeLists(classDecl.AttributeLists)
            .WithMembers(SyntaxFactory.List(newMembers))
            .WithLeadingTrivia(classDecl.GetLeadingTrivia())
            .WithTrailingTrivia(classDecl.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static SyntaxNode RewriteSameDocumentCallSites(
        SyntaxNode root,
        SyntaxAnnotation callSiteAnnotation,
        List<string> parameterNames
    )
    {
        var annotatedCallSites = root.GetAnnotatedNodes(callSiteAnnotation)
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(c => c.ArgumentList is { Arguments.Count: > 0 })
            .ToList();

        if (annotatedCallSites.Count is 0)
            return root;

        return root.ReplaceNodes(
            annotatedCallSites,
            (original, _) => RewriteCallSite(original, parameterNames).WithAdditionalAnnotations(Formatter.Annotation)
        );
    }

    private static async Task<Solution> RewriteOtherDocumentCallSitesAsync(
        Solution solution,
        Dictionary<DocumentId, List<SyntaxNode>> otherDocCallSites,
        List<string> parameterNames,
        CancellationToken ct
    )
    {
        foreach (var kvp in otherDocCallSites)
        {
            var targetDoc = solution.GetDocument(kvp.Key);
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

            if (creationsToRewrite.Count <= 0)
                continue;

            var rewrittenRoot = targetRoot.ReplaceNodes(
                creationsToRewrite,
                (original, _) =>
                    RewriteCallSite(original, parameterNames).WithAdditionalAnnotations(Formatter.Annotation)
            );
            solution = solution.WithDocumentSyntaxRoot(kvp.Key, rewrittenRoot);
        }

        return solution;
    }

    private static IMethodSymbol? FindPrimaryConstructorSymbol(INamedTypeSymbol typeSymbol, CancellationToken ct)
    {
        foreach (var ctor in typeSymbol.Constructors)
        {
            if (ctor.IsImplicitlyDeclared || ctor.IsStatic)
                continue;

            var isPrimary = ctor.DeclaringSyntaxReferences.All(r =>
                r.GetSyntax(ct) is not ConstructorDeclarationSyntax
            );

            if (isPrimary)
                return ctor;
        }

        return null;
    }

    private static ConstructorDeclarationSyntax? FindSingleConstructor(ClassDeclarationSyntax classDecl)
    {
        ConstructorDeclarationSyntax? found = null;

        foreach (var member in classDecl.Members)
        {
            if (member is not ConstructorDeclarationSyntax ctor)
                continue;

            if (found is not null)
                return null;

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
            constructor.ExpressionBody?.Expression is AssignmentExpressionSyntax
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
            newProperty = newProperty
                .WithInitializer(SyntaxFactory.EqualsValueClause(assignment.DefaultValue.WithoutTrivia()))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }
        else if (property.Modifiers.Any(m => m.IsKind(SyntaxKind.RequiredKeyword)) is false)
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

        if (needsInit is false)
            return property.WithAccessorList(property.AccessorList.WithAccessors(SyntaxFactory.List(newAccessors)));

        var lastAccessor = newAccessors[newAccessors.Count - 1];
        newAccessors.Add(
            SyntaxFactory
                .AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(lastAccessor.GetLeadingTrivia())
                .WithTrailingTrivia(lastAccessor.GetTrailingTrivia())
        );

        return property.WithAccessorList(property.AccessorList.WithAccessors(SyntaxFactory.List(newAccessors)));
    }

    private static PropertyDeclarationSyntax ConvertPropertyToImmutable(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is null)
            return property;

        var hasDefault = property.Initializer is not null;
        var hasSetter = property.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        var hasInit = property.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));

        if (hasSetter is false && hasInit is false)
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

    private static ObjectCreationExpressionSyntax RewriteCallSite(
        ObjectCreationExpressionSyntax creation,
        List<string> propertyNames
    )
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

        if (property.Modifiers.Any(m => m.IsKind(SyntaxKind.RequiredKeyword)) is true)
            return newProperty;

        var requiredToken = SyntaxFactory.Token(SyntaxKind.RequiredKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        var publicIndex = newProperty.Modifiers.IndexOf(SyntaxKind.PublicKeyword);
        newProperty =
            publicIndex >= 0
                ? newProperty.WithModifiers(newProperty.Modifiers.Insert(publicIndex + 1, requiredToken))
                : newProperty.AddModifiers(requiredToken);

        return newProperty;
    }

    private static bool IsMemberRemovedByRecordConversion(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax { Identifier.Text: "Equals" } method => method.ParameterList.Parameters.Count == 1
                && method.ParameterList.Parameters[0].Type
                    is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword }
                        or NullableTypeSyntax
                        {
                            ElementType: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword },
                        },
            OperatorDeclarationSyntax op => op.OperatorToken.Kind()
                is SyntaxKind.EqualsEqualsToken
                    or SyntaxKind.ExclamationEqualsToken,
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
