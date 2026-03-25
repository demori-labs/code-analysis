using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class ConstantPatternAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public enum Status { Active, Inactive, Pending }

            public class Service
            {
                public void Process(object? obj, int code, string? name, Status status)
                {
                    if (obj == null) return;
                    if (obj != null) { }
                    if (code == 42) { }
                    if (code != 0) { }
                    if (name == "admin") { }
                    if (status == Status.Active) { }
                    if (null == obj) { }
                    var isActive = status != Status.Inactive;
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new ConstantPatternAnalyzer());
    }
}
