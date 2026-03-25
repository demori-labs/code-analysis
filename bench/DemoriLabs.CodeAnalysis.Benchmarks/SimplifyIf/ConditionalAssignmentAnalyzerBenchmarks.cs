using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class ConditionalAssignmentAnalyzerBenchmarks
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
                    int x;
                    if (cond) x = 1; else x = 2;
                }

                public void SetB(bool cond)
                {
                    int x;
                    if (cond) { x = 1; } else { x = 2; }
                }

                public void SetC(bool cond, int a, int b)
                {
                    int x;
                    if (cond) x = a + 1; else x = b + 1;
                }

                public void SetD(bool cond)
                {
                    string x;
                    if (cond) x = "hello"; else x = "world";
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new ConditionalAssignmentAnalyzer());
    }
}
