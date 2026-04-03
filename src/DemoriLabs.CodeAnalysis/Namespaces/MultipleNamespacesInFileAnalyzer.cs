using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Namespaces;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultipleNamespacesInFileAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.MultipleNamespacesInFile,
        title: "File contains multiple different namespaces",
        messageFormat: "File contains multiple different namespaces",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A file should contain only one namespace. Split types into separate files matching their namespace."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(static syntaxTreeContext => AnalyzeSyntaxTree(syntaxTreeContext));
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);

        var topLevelNamespaces = root.ChildNodes().OfType<NamespaceDeclarationSyntax>().ToList();
        if (topLevelNamespaces.Count is 0)
            return;

        // Case 1: Multiple top-level namespaces with different names
        var distinctNames = topLevelNamespaces
            .Select(static ns => ns.Name.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (distinctNames.Count > 1)
        {
            foreach (var ns in topLevelNamespaces)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, ns.Name.GetLocation()));
            }

            return;
        }

        // Case 2: Nested namespace where outer has direct members
        foreach (var ns in topLevelNamespaces)
        {
            if (HasDirectMembersAndNestedNamespaces(ns))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, ns.Name.GetLocation()));
            }
        }
    }

    private static bool HasDirectMembersAndNestedNamespaces(NamespaceDeclarationSyntax ns)
    {
        var hasDirectMembers = false;
        var hasChildNamespaces = false;

        foreach (var member in ns.Members)
        {
            if (member is NamespaceDeclarationSyntax)
            {
                hasChildNamespaces = true;
            }
            else
            {
                hasDirectMembers = true;
            }

            if (hasDirectMembers && hasChildNamespaces)
                return true;
        }

        return false;
    }
}
