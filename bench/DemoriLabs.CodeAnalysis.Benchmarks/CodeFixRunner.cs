using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DemoriLabs.CodeAnalysis.Benchmarks;

internal sealed class CodeFixRunner
{
    private readonly Document _document;
    private readonly ImmutableArray<Diagnostic> _diagnostics;

    private CodeFixRunner(Document document, ImmutableArray<Diagnostic> diagnostics)
    {
        _document = document;
        _diagnostics = diagnostics;
    }

    public static async Task<CodeFixRunner> CreateAsync<TAnalyzer>(
        string source,
        params MetadataReference[] additionalReferences
    )
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var adhocWorkspace = new AdhocWorkspace();

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(MetadataReference (path) => MetadataReference.CreateFromFile(path))
            .ToList();

        references.AddRange(additionalReferences);

        var project = adhocWorkspace
            .AddProject("BenchmarkProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest))
            .WithMetadataReferences(references);

        adhocWorkspace.TryApplyChanges(project.Solution);

        var document = adhocWorkspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));

        var compilation = (await document.Project.GetCompilationAsync())!;
        var withAnalyzers = compilation.WithAnalyzers([new TAnalyzer()]);
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        return new CodeFixRunner(document, diagnostics);
    }

    public async Task<Solution> ApplyFixAsync(CodeFixProvider codeFix)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            _document,
            _diagnostics[0],
            (action, _) => actions.Add(action),
            CancellationToken.None
        );

        await codeFix.RegisterCodeFixesAsync(context);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        return ((ApplyChangesOperation)operations[0]).ChangedSolution;
    }
}
