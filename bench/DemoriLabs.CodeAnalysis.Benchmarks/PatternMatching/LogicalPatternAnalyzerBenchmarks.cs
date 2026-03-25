using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class LogicalPatternAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public void Process(int x, string s)
                {
                    if (x == 1 || x == 2 || x == 3) { }
                    if (x >= 0 && x < 100) { }
                    if (x != 1 && x != 2) { }
                    if (s == "a" || s == "b") { }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new LogicalPatternAnalyzer());
    }
}
