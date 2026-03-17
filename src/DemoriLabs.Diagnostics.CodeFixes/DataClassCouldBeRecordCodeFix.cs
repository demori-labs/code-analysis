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
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to record",
                ct => ConvertToRecordAsync(context.Document, classDecl, ct),
                equivalenceKey: "ConvertToRecord"
            ),
            diagnostic
        );
    }

    private static async Task<Document> ConvertToRecordAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newMembers = new List<MemberDeclarationSyntax>();

        foreach (var member in classDecl.Members)
        {
            if (member is PropertyDeclarationSyntax property)
            {
                newMembers.Add(ConvertPropertyToImmutable(property));
            }
            else
            {
                newMembers.Add(member);
            }
        }

        var recordKeyword = SyntaxFactory.Token(SyntaxKind.RecordKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        var recordDecl = SyntaxFactory
            .RecordDeclaration(recordKeyword, classDecl.Identifier)
            .WithModifiers(classDecl.Modifiers)
            .WithTypeParameterList(classDecl.TypeParameterList)
            .WithBaseList(classDecl.BaseList)
            .WithConstraintClauses(classDecl.ConstraintClauses)
            .WithOpenBraceToken(classDecl.OpenBraceToken)
            .WithCloseBraceToken(classDecl.CloseBraceToken)
            .WithSemicolonToken(classDecl.SemicolonToken)
            .WithAttributeLists(classDecl.AttributeLists)
            .WithMembers(SyntaxFactory.List(newMembers))
            .WithLeadingTrivia(classDecl.GetLeadingTrivia())
            .WithTrailingTrivia(classDecl.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(classDecl, recordDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    private static PropertyDeclarationSyntax ConvertPropertyToImmutable(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is null)
        {
            return property;
        }

        var hasDefault = property.Initializer is not null;
        var hasSetter = property.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        var hasInit = property.AccessorList.Accessors.Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));

        if (!hasSetter && !hasInit)
        {
            return property;
        }

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
}
