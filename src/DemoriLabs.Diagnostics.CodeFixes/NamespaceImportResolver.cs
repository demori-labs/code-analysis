using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.Diagnostics.CodeFixes;

internal static class NamespaceImportResolver
{
    internal static SyntaxNode EnsureUsingDirective(
        this SyntaxNode root,
        SemanticModel semanticModel,
        string namespaceName
    )
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (IsNamespaceImported(compilationUnit, semanticModel.Compilation, namespaceName))
        {
            return root;
        }

        var isFirst = compilationUnit.Usings.Count == 0;

        var usingDirective = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .NormalizeWhitespace()
            .WithTrailingTrivia(
                isFirst
                    ? SyntaxFactory.TriviaList(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed)
                    : SyntaxFactory.TriviaList(SyntaxFactory.LineFeed)
            );

        var newUsings = compilationUnit.Usings.Add(usingDirective);
        return compilationUnit.WithUsings(newUsings);
    }

    private static bool IsNamespaceImported(
        CompilationUnitSyntax compilationUnit,
        Compilation compilation,
        string namespaceName
    )
    {
        if (
            compilationUnit.Usings.Any(u =>
                string.Equals(u.Name?.ToString(), namespaceName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return true;
        }

        return compilation
            .SyntaxTrees.Select(tree => tree.GetRoot())
            .OfType<CompilationUnitSyntax>()
            .SelectMany(unit => unit.Usings)
            .Any(u =>
                u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                && string.Equals(u.Name?.ToString(), namespaceName, StringComparison.OrdinalIgnoreCase)
            );
    }
}
