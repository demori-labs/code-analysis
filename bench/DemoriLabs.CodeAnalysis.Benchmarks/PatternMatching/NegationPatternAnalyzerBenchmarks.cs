using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class NegationPatternAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public void Process(bool flag, object? obj, string? name)
                {
                    if (!flag) { }
                    if (!string.IsNullOrEmpty(name)) { }
                    if (!(obj is string)) { }
                    if (!(obj is null)) { }
                    var result = !flag;
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new NegationPatternAnalyzer());
    }
}
