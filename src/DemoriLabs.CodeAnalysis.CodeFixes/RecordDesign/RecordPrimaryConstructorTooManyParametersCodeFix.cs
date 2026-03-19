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
public sealed class RecordPrimaryConstructorTooManyParametersCodeFix : CodeFixProvider
{
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

    private static async Task<Document> ConvertToExplicitPropertiesAsync(
        Document document,
        RecordDeclarationSyntax record,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null || record.ParameterList is null)
            return document;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        var indent = GetIndent(document, record.SyntaxTree);

        // Record structs are mutable; readonly record structs and record classes are immutable
        var isMutableRecordStruct =
            record.IsKind(SyntaxKind.RecordStructDeclaration)
            && !record.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
        var accessorKind = isMutableRecordStruct
            ? SyntaxKind.SetAccessorDeclaration
            : SyntaxKind.InitAccessorDeclaration;

        // Identify constructors that chain to the primary constructor
        var primaryCtorChainers = new HashSet<ConstructorDeclarationSyntax>();
        if (semanticModel is not null)
        {
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
        }

        var hasChainersToPrimary = primaryCtorChainers.Count > 0;

        // Build properties from positional parameters
        var parameterNames = new List<string>();
        var properties = new List<MemberDeclarationSyntax>();

        foreach (var parameter in record.ParameterList.Parameters)
        {
            if (parameter.Type is null)
            {
                continue;
            }

            var propertyName =
                char.ToUpperInvariant(parameter.Identifier.Text[0]) + parameter.Identifier.Text.Substring(1);

            parameterNames.Add(propertyName);

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

            // Migrate parameter attributes to the property, skipping parameter-only attributes
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
                // NormalizeWhitespace produces "[Attr]\r\npublic ..." with no indentation.
                // We need to indent both the attribute line and the property keyword line,
                // and normalise line endings to \n.
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

        // Rewrite constructors that chain to primary; keep others as-is
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
                    // Non-primary-chaining constructor: keep but add blank line separator
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
            .WithMembers(allMembers);

        var newRoot = root.ReplaceNode(record, newRecord);
        return document.WithSyntaxRoot(newRoot);
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

    private static string GetIndent(Document document, SyntaxTree syntaxTree)
    {
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

        var useTabs =
            options.TryGetValue("indent_style", out var style)
            && string.Equals(style, "tab", StringComparison.OrdinalIgnoreCase);

        if (useTabs)
        {
            return "\t";
        }

        if (options.TryGetValue("indent_size", out var sizeValue) && int.TryParse(sizeValue, out var size))
        {
            return new string(' ', size);
        }

        return "    ";
    }

    private static SyntaxList<AttributeListSyntax> FilterAttributeLists(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel? semanticModel,
        CancellationToken ct
    )
    {
        if (attributeLists.Count == 0)
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

        // Check AttributeUsage to see if this attribute can target properties.
        // If it can only target parameters (and not properties), it should be dropped.
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
