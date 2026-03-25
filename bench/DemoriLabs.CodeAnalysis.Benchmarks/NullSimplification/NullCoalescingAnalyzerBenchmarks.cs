using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NullSimplification;

[MemoryDiagnoser]
public class NullCoalescingAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public string CheckA(string? x, string y)
                {
                    return x != null ? x : y;
                }

                public string CheckB(string? x, string y)
                {
                    return x == null ? y : x;
                }

                public string CheckC(string? x, string y)
                {
                    if (x != null) return x;
                    return y;
                }

                public string CheckD(string? x, string y)
                {
                    return x is not null ? x : y;
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new NullCoalescingAnalyzer());
    }
}
