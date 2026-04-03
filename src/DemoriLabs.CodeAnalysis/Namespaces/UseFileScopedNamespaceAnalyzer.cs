using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Namespaces;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseFileScopedNamespaceAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseFileScopedNamespace,
        title: "Use file-scoped namespace declaration",
        messageFormat: "Use file-scoped namespace declaration",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Block-scoped namespace declarations should be converted to file-scoped namespace declarations."
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

        // If there's already a file-scoped namespace, nothing to do
        if (root.ChildNodes().OfType<FileScopedNamespaceDeclarationSyntax>().Any())
            return;

        var topLevelNamespaces = root.ChildNodes().OfType<NamespaceDeclarationSyntax>().ToList();
        if (topLevelNamespaces.Count is 0)
            return;

        // Check if all top-level namespaces can be flattened to the same effective namespace
        var flattenedNames = new List<string>(topLevelNamespaces.Count);
        foreach (var ns in topLevelNamespaces)
        {
            var flattenedName = TryFlatten(ns);
            if (flattenedName is null)
                return;

            flattenedNames.Add(flattenedName);
        }

        // All must resolve to the same namespace
        var firstName = flattenedNames[0];
        for (var i = 1; i < flattenedNames.Count; i++)
        {
            if (string.Equals(flattenedNames[i], firstName, StringComparison.Ordinal) is false)
                return;
        }

        // Report on each top-level block namespace
        var properties = ImmutableDictionary.CreateRange([
            new KeyValuePair<string, string?>("FlattenedNamespace", firstName),
        ]);

        foreach (var ns in topLevelNamespaces)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, ns.Name.GetLocation(), properties));
        }
    }

    /// <summary>
    /// Attempts to flatten a namespace declaration into a single dotted name.
    /// Returns null if the namespace has direct members AND nested namespaces (DL3020 territory).
    /// </summary>
    private static string? TryFlatten(NamespaceDeclarationSyntax ns)
    {
        var hasDirectMembers = HasDirectMembers(ns);
        var childNamespaces = ns.Members.OfType<NamespaceDeclarationSyntax>().ToList();

        if (hasDirectMembers && childNamespaces.Count > 0)
            return null;

        if (childNamespaces.Count is 0)
            return ns.Name.ToString();

        if (childNamespaces.Count > 1)
            return null;

        var innerFlattened = TryFlatten(childNamespaces[0]);
        if (innerFlattened is null)
            return null;

        return ns.Name + "." + innerFlattened;
    }

    private static bool HasDirectMembers(NamespaceDeclarationSyntax ns)
    {
        foreach (var member in ns.Members)
        {
            if (member is not NamespaceDeclarationSyntax)
                return true;
        }

        return false;
    }
}
