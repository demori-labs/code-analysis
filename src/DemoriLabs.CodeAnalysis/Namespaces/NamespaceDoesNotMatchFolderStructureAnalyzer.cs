using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Namespaces;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamespaceDoesNotMatchFolderStructureAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.NamespaceDoesNotMatchFolderStructure,
        title: "Namespace does not match folder structure",
        messageFormat: "Namespace '{0}' does not match folder structure (expected '{1}')",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Namespace declarations should match the folder structure relative to the project root."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var globalOptions = compilationContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions;

            if (
                globalOptions.TryGetValue("build_property.ProjectDir", out var projectDir) is false
                || string.IsNullOrWhiteSpace(projectDir)
            )
            {
                return;
            }

            if (
                globalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace) is false
                || string.IsNullOrWhiteSpace(rootNamespace)
            )
            {
                rootNamespace = compilationContext.Compilation.AssemblyName ?? string.Empty;
            }

            if (string.IsNullOrEmpty(rootNamespace))
                return;

            var normalizedProjectDir = NormalizeDirectory(projectDir);

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeNamespace(analysisContext, normalizedProjectDir, rootNamespace),
                SyntaxKind.NamespaceDeclaration,
                SyntaxKind.FileScopedNamespaceDeclaration
            );
        });
    }

    private static void AnalyzeNamespace(SyntaxNodeAnalysisContext context, string projectDir, string rootNamespace)
    {
        var namespaceDeclaration = (BaseNamespaceDeclarationSyntax)context.Node;

        // Skip nested namespace declarations — only check the outermost
        if (namespaceDeclaration.Parent is BaseNamespaceDeclarationSyntax)
            return;

        var filePath = context.Node.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(filePath))
            return;

        var normalizedFilePath = NormalizePath(filePath);
        if (normalizedFilePath.StartsWith(projectDir, StringComparison.Ordinal) is false)
            return;

        var relativePath = normalizedFilePath.Substring(projectDir.Length);
        var expectedNamespace = BuildExpectedNamespace(rootNamespace, relativePath);
        var declaredNamespace = namespaceDeclaration.Name.ToString();

        if (string.Equals(declaredNamespace, expectedNamespace, StringComparison.Ordinal))
            return;

        var properties = ImmutableDictionary.CreateRange([
            new KeyValuePair<string, string?>("ExpectedNamespace", expectedNamespace),
        ]);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                namespaceDeclaration.Name.GetLocation(),
                properties,
                declaredNamespace,
                expectedNamespace
            )
        );
    }

    private static string BuildExpectedNamespace(string rootNamespace, string relativePath)
    {
        var directoryPart = GetDirectoryPart(relativePath);
        if (string.IsNullOrEmpty(directoryPart))
            return rootNamespace;

        var namespaceSuffix = directoryPart.Replace('/', '.');
        return rootNamespace + "." + namespaceSuffix;
    }

    private static string GetDirectoryPart(string relativePath)
    {
        var lastSlash = relativePath.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : relativePath.Substring(0, lastSlash);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string NormalizeDirectory(string path)
    {
        var normalized = NormalizePath(path);
        if (normalized.Length > 0 && normalized[normalized.Length - 1] is not '/')
            normalized += '/';

        return normalized;
    }
}
