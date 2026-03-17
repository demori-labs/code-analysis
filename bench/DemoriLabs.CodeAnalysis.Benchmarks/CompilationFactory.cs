using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Benchmarks;

internal static class CompilationFactory
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> PlatformReferences = new(() =>
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        return references;
    });

    public static CSharpCompilation CreateCompilation(string source, params MetadataReference[] additionalReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = PlatformReferences.Value;

        if (additionalReferences.Length > 0)
            references = references.AddRange(additionalReferences);

        return CSharpCompilation.Create(
            "BenchmarkAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        CSharpCompilation compilation,
        DiagnosticAnalyzer analyzer
    )
    {
        var withAnalyzers = compilation.WithAnalyzers([analyzer]);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
