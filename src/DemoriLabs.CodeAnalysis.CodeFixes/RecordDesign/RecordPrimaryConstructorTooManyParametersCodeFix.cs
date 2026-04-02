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
public sealed class RecordPrimaryConstructorTooManyParametersCodeFix : CodeFixProvider
{
    private static readonly SyntaxAnnotation RecordAnnotation = new("RecordToConvert");

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RuleIdentifiers.RecordPrimaryConstructorTooManyParameters];

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

        var record = node.FirstAncestorOrSelf<RecordDeclarationSyntax>();
        if (record?.ParameterList is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to explicit required properties",
                ct => ConvertToExplicitPropertiesAsync(context.Document, record, ct),
                equivalenceKey: "ConvertToExplicitRequiredProperties"
            ),
            diagnostic
        );
    }

    private static async Task<Solution> ConvertToExplicitPropertiesAsync(
        Document document,
        RecordDeclarationSyntax record,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null || record.ParameterList is null)
            return document.Project.Solution;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null)
            return document.Project.Solution;

        // Find the primary constructor symbol before any modifications
        var recordSymbol = semanticModel.GetDeclaredSymbol(record, ct);
        if (recordSymbol is null)
            return document.Project.Solution;

        var primaryCtor = FindPrimaryConstructor(recordSymbol, ct);

        // Build parameter-to-property name mapping
        var parameterNames = new List<string>();
        var parameterToPropertyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in record.ParameterList.Parameters)
        {
            if (parameter.Type is null)
                continue;

            var propertyName =
                char.ToUpperInvariant(parameter.Identifier.Text[0]) + parameter.Identifier.Text.Substring(1);
            parameterNames.Add(propertyName);
            parameterToPropertyMap[parameter.Identifier.Text] = propertyName;
        }

        // Find all references to the primary constructor across the solution
        var callSitesByDocument = new Dictionary<DocumentId, List<SyntaxNode>>();

        if (primaryCtor is not null)
        {
            var solution = document.Project.Solution;
            var references = await SymbolFinder.FindReferencesAsync(primaryCtor, solution, ct).ConfigureAwait(false);

            foreach (var reference in references)
            {
                foreach (var location in reference.Locations)
                {
                    if (location.Document.Id == document.Id)
                    {
                        // Same document — find the node in current root
                        var callNode = root.FindNode(location.Location.SourceSpan);
                        var creation = callNode.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
                        if (creation?.ArgumentList is { Arguments.Count: > 0 })
                        {
                            if (!callSitesByDocument.TryGetValue(location.Document.Id, out var list))
                            {
                                list = [];
                                callSitesByDocument[location.Document.Id] = list;
                            }

                            list.Add(creation);
                        }

                        continue;
                    }

                    if (!callSitesByDocument.TryGetValue(location.Document.Id, out var docList))
                    {
                        docList = [];
                        callSitesByDocument[location.Document.Id] = docList;
                    }

                    var sourceRoot = await location.Location.SourceTree!.GetRootAsync(ct).ConfigureAwait(false);
                    docList.Add(sourceRoot.FindNode(location.Location.SourceSpan));
                }
            }
        }

        // Annotate the record so we can find it after call-site rewrites in the same document
        var annotatedRecord = record.WithAdditionalAnnotations(RecordAnnotation);
        var annotatedRoot = root.ReplaceNode(record, annotatedRecord);
        var currentSolution = document.Project.Solution.WithDocumentSyntaxRoot(document.Id, annotatedRoot);

        // Rewrite call sites in the same document as the record
        if (callSitesByDocument.TryGetValue(document.Id, out var sameDocCallSites))
        {
            var currentDoc = currentSolution.GetDocument(document.Id)!;
            var currentRoot = await currentDoc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (currentRoot is not null)
            {
                // Map old nodes to their locations in the annotated root
                var creationsToRewrite = new List<ObjectCreationExpressionSyntax>();
                foreach (var oldNode in sameDocCallSites)
                {
                    if (oldNode is not ObjectCreationExpressionSyntax creation)
                        continue;

                    var nodeInCurrent = currentRoot.FindNode(creation.Span);
                    var creationInCurrent = nodeInCurrent.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
                    if (creationInCurrent?.ArgumentList is { Arguments.Count: > 0 })
                        creationsToRewrite.Add(creationInCurrent);
                }

                if (creationsToRewrite.Count > 0)
                {
                    var newRoot = currentRoot.ReplaceNodes(
                        creationsToRewrite,
                        (original, _) => RewriteCallSite(original, parameterNames, parameterToPropertyMap)
                    );
                    currentSolution = currentSolution.WithDocumentSyntaxRoot(document.Id, newRoot);
                }
            }

            callSitesByDocument.Remove(document.Id);
        }

        // Rewrite call sites in other documents
        foreach (var kvp in callSitesByDocument)
        {
            var otherDoc = currentSolution.GetDocument(kvp.Key);
            if (otherDoc is null)
                continue;

            var otherRoot = await otherDoc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (otherRoot is null)
                continue;

            var creationsToRewrite = kvp
                .Value.Select(n => n.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>())
                .Where(c => c?.ArgumentList is { Arguments.Count: > 0 })
                .Cast<ObjectCreationExpressionSyntax>()
                .ToList();

            if (creationsToRewrite.Count <= 0)
                continue;

            var newRoot = otherRoot.ReplaceNodes(
                creationsToRewrite,
                (original, _) => RewriteCallSite(original, parameterNames, parameterToPropertyMap)
            );
            currentSolution = currentSolution.WithDocumentSyntaxRoot(kvp.Key, newRoot);
        }

        // Now apply the record declaration transformation
        var finalDoc = currentSolution.GetDocument(document.Id)!;
        var finalRoot = await finalDoc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (finalRoot is null)
            return currentSolution;

        var recordToTransform = finalRoot
            .GetAnnotatedNodes(RecordAnnotation)
            .OfType<RecordDeclarationSyntax>()
            .FirstOrDefault();

        if (recordToTransform is null)
            return currentSolution;

        var finalSemanticModel = await finalDoc.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (finalSemanticModel is null)
            return currentSolution;

        var transformedRoot = RewriteRecordDeclaration(
            finalRoot,
            recordToTransform,
            finalDoc,
            finalSemanticModel,
            parameterNames,
            ct
        );

        return currentSolution.WithDocumentSyntaxRoot(document.Id, transformedRoot);
    }

    private static IMethodSymbol? FindPrimaryConstructor(INamedTypeSymbol recordSymbol, CancellationToken ct)
    {
        foreach (var ctor in recordSymbol.Constructors)
        {
            if (ctor.IsImplicitlyDeclared)
                continue;

            var isPrimary = ctor.DeclaringSyntaxReferences.All(r =>
                r.GetSyntax(ct) is not ConstructorDeclarationSyntax
            );

            if (isPrimary)
                return ctor;
        }

        return null;
    }

    private static ObjectCreationExpressionSyntax RewriteCallSite(
        ObjectCreationExpressionSyntax creation,
        List<string> parameterNames,
        Dictionary<string, string> parameterToPropertyMap
    )
    {
        if (creation.ArgumentList is null)
            return creation;

        var args = creation.ArgumentList.Arguments;
        var assignments = new List<ExpressionSyntax>();

        for (var i = 0; i < args.Count && i < parameterNames.Count; i++)
        {
            var arg = args[i];

            string propertyName;
            if (arg.NameColon is not null)
            {
                var paramName = arg.NameColon.Name.Identifier.Text;
                propertyName = parameterToPropertyMap.TryGetValue(paramName, out var mapped) ? mapped : paramName;
            }
            else
            {
                propertyName = parameterNames[i];
            }

            assignments.Add(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(propertyName),
                    arg.Expression.WithoutTrivia()
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
            .WithTrailingTrivia(creation.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static SyntaxNode RewriteRecordDeclaration(
        SyntaxNode root,
        RecordDeclarationSyntax record,
        Document document,
        SemanticModel semanticModel,
        List<string> parameterNames,
        CancellationToken ct
    )
    {
        if (record.ParameterList is null)
            return root;

        var indent = IndentationResolver.GetIndentUnit(document, record.SyntaxTree);

        var isMutableRecordStruct =
            record.IsKind(SyntaxKind.RecordStructDeclaration)
            && !record.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
        var accessorKind = isMutableRecordStruct
            ? SyntaxKind.SetAccessorDeclaration
            : SyntaxKind.InitAccessorDeclaration;

        var primaryCtorChainers = new HashSet<ConstructorDeclarationSyntax>();
        foreach (var member in record.Members)
        {
            if (
                member is ConstructorDeclarationSyntax { Initializer: { } init } ctor
                && init.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword)
                && semanticModel.GetSymbolInfo(init, ct).Symbol is IMethodSymbol symbol
                && !symbol.DeclaringSyntaxReferences.Any(r => r.GetSyntax(ct) is ConstructorDeclarationSyntax)
            )
            {
                primaryCtorChainers.Add(ctor);
            }
        }

        var hasChainersToPrimary = primaryCtorChainers.Count > 0;

        var properties = new List<MemberDeclarationSyntax>();

        foreach (var parameter in record.ParameterList.Parameters)
        {
            if (parameter.Type is null)
                continue;

            var propertyName =
                char.ToUpperInvariant(parameter.Identifier.Text[0]) + parameter.Identifier.Text.Substring(1);

            var hasDefault = parameter.Default is not null;
            var addRequired = !hasDefault && !hasChainersToPrimary;

            var property = SyntaxFactory
                .PropertyDeclaration(parameter.Type.WithoutTrivia(), SyntaxFactory.Identifier(propertyName))
                .AddModifiers(
                    addRequired
                        ?
                        [
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.RequiredKeyword),
                        ]
                        : [SyntaxFactory.Token(SyntaxKind.PublicKeyword)]
                )
                .AddAccessorListAccessors(
                    SyntaxFactory
                        .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory
                        .AccessorDeclaration(accessorKind)
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                );

            var attributeLists = FilterAttributeLists(parameter.AttributeLists, semanticModel, ct);
            if (attributeLists.Count > 0)
                property = property.WithAttributeLists(attributeLists);

            if (hasDefault)
            {
                property = property
                    .WithInitializer(SyntaxFactory.EqualsValueClause(parameter.Default!.Value.WithoutTrivia()))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }

            property = property.NormalizeWhitespace();

            if (property.AttributeLists.Count > 0)
            {
                var newAttrLists = new List<AttributeListSyntax>();
                foreach (var attrList in property.AttributeLists)
                {
                    var closeBracket = attrList.CloseBracketToken.WithTrailingTrivia(SyntaxFactory.LineFeed);
                    newAttrLists.Add(attrList.WithCloseBracketToken(closeBracket));
                }

                var firstModifier = property.Modifiers.First();
                property = property
                    .WithAttributeLists(SyntaxFactory.List(newAttrLists))
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(indent))
                    .WithModifiers(
                        property.Modifiers.Replace(
                            firstModifier,
                            firstModifier.WithLeadingTrivia(SyntaxFactory.Whitespace(indent))
                        )
                    )
                    .WithTrailingTrivia(SyntaxFactory.LineFeed);
            }
            else
            {
                property = property
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(indent))
                    .WithTrailingTrivia(SyntaxFactory.LineFeed);
            }

            properties.Add(property);
        }

        var rewrittenMembers = new List<MemberDeclarationSyntax>();

        foreach (var member in record.Members)
        {
            switch (member)
            {
                case ConstructorDeclarationSyntax ctor when primaryCtorChainers.Contains(ctor):
                    rewrittenMembers.Add(RewriteConstructor(ctor, ctor.Initializer!, parameterNames, indent));
                    break;
                case ConstructorDeclarationSyntax nonPrimaryCtor when hasChainersToPrimary:
                {
                    var kept = nonPrimaryCtor.WithLeadingTrivia(
                        SyntaxFactory.LineFeed,
                        SyntaxFactory.Whitespace(indent)
                    );

                    if (kept.Body is { Statements.Count: 0 } emptyBody)
                    {
                        kept = kept.WithBody(
                            emptyBody
                                .WithOpenBraceToken(emptyBody.OpenBraceToken.WithTrailingTrivia())
                                .WithCloseBraceToken(emptyBody.CloseBraceToken.WithLeadingTrivia())
                        );
                    }

                    rewrittenMembers.Add(kept);
                    break;
                }
                default:
                    rewrittenMembers.Add(member);
                    break;
            }
        }

        var openBrace = SyntaxFactory
            .Token(SyntaxKind.OpenBraceToken)
            .WithLeadingTrivia(SyntaxFactory.LineFeed)
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        var closeBrace = record.CloseBraceToken.IsKind(SyntaxKind.None)
            ? SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
            : record.CloseBraceToken;

        var allMembers = SyntaxFactory.List(properties.Concat(rewrittenMembers));

        var newRecord = record
            .WithParameterList(null)
            .WithOpenBraceToken(openBrace)
            .WithCloseBraceToken(closeBrace)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
            .WithMembers(allMembers)
            .WithoutAnnotations(RecordAnnotation);

        return root.ReplaceNode(record, newRecord);
    }

    private static ConstructorDeclarationSyntax RewriteConstructor(
        ConstructorDeclarationSyntax ctor,
        ConstructorInitializerSyntax initializer,
        List<string> parameterNames,
        string indent
    )
    {
        var assignments = new List<StatementSyntax>();
        var args = initializer.ArgumentList.Arguments;

        for (var i = 0; i < args.Count && i < parameterNames.Count; i++)
        {
            var assignment = SyntaxFactory
                .ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(parameterNames[i]),
                        args[i].Expression.WithoutTrivia()
                    )
                )
                .NormalizeWhitespace()
                .WithLeadingTrivia(SyntaxFactory.Whitespace(indent + indent))
                .WithTrailingTrivia(SyntaxFactory.LineFeed);

            assignments.Add(assignment);
        }

        var existingStatements = ctor.Body?.Statements ?? default;
        var allStatements = SyntaxFactory.List(assignments.Concat(existingStatements));

        var newBody = SyntaxFactory
            .Block(allStatements)
            .WithOpenBraceToken(
                SyntaxFactory
                    .Token(SyntaxKind.OpenBraceToken)
                    .WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.Whitespace(indent))
                    .WithTrailingTrivia(SyntaxFactory.LineFeed)
            )
            .WithCloseBraceToken(
                SyntaxFactory
                    .Token(SyntaxKind.CloseBraceToken)
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(indent))
                    .WithTrailingTrivia(SyntaxFactory.LineFeed)
            );

        var cleanParamList = ctor.ParameterList.WithCloseParenToken(
            ctor.ParameterList.CloseParenToken.WithTrailingTrivia(SyntaxFactory.TriviaList())
        );

        return ctor.WithParameterList(cleanParamList)
            .WithInitializer(null)
            .WithBody(newBody)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
            .WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.Whitespace(indent));
    }

    private static SyntaxList<AttributeListSyntax> FilterAttributeLists(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel? semanticModel,
        CancellationToken ct
    )
    {
        if (attributeLists.Count is 0)
            return attributeLists;

        var result = new List<AttributeListSyntax>();

        foreach (var attrList in attributeLists)
        {
            var kept = new List<AttributeSyntax>();

            foreach (var attr in attrList.Attributes)
            {
                if (IsParameterOnlyAttribute(attr, semanticModel, ct))
                    continue;

                kept.Add(attr.WithoutTrivia());
            }

            if (kept.Count > 0)
                result.Add(SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(kept)));
        }

        return SyntaxFactory.List(result);
    }

    private static bool IsParameterOnlyAttribute(
        AttributeSyntax attribute,
        SemanticModel? semanticModel,
        CancellationToken ct
    )
    {
        if (semanticModel is null)
            return false;

        var symbolInfo = semanticModel.GetSymbolInfo(attribute, ct);
        var attributeType = (symbolInfo.Symbol as IMethodSymbol)?.ContainingType;

        if (attributeType is null)
            return false;

        var usageAttr = attributeType
            .GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.Name == "AttributeUsageAttribute"
                && a.AttributeClass.ContainingNamespace?.ToDisplayString() == "System"
            );

        if (usageAttr is not { ConstructorArguments: { Length: > 0 } args } || args[0].Value is not int targets)
            return false;

        var validTargets = (AttributeTargets)targets;
        return validTargets.HasFlag(AttributeTargets.Parameter) && !validTargets.HasFlag(AttributeTargets.Property);
    }
}
