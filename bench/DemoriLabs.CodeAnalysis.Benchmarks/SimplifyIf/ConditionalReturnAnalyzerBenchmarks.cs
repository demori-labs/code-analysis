using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class ConditionalReturnAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public int PickA(bool cond)
                {
                    if (cond) return 1; else return 2;
                }

                public int PickB(bool cond)
                {
                    if (cond) return 1;
                    return 2;
                }

                public int PickC(bool a, bool b)
                {
                    if (a && b) { return 1; }
                    return 2;
                }

                public int PickD(bool cond)
                {
                    if (cond) { return 1; } else { return 2; }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new ConditionalReturnAnalyzer());
    }
}
