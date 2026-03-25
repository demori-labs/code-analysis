using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class BooleanAssignmentAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public void SetA(bool cond)
                {
                    bool x;
                    if (cond) x = true; else x = false;
                }

                public void SetB(bool cond)
                {
                    bool x;
                    if (cond) x = false; else x = true;
                }

                public void SetC(bool a, bool b)
                {
                    bool x;
                    if (a && b) { x = true; } else { x = false; }
                }

                public void SetD(bool cond)
                {
                    bool x;
                    if (cond) { x = false; } else { x = true; }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new BooleanAssignmentAnalyzer());
    }
}
