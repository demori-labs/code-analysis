using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class BooleanReturnAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public bool CheckA(bool cond)
                {
                    if (cond) return true; else return false;
                }

                public bool CheckB(bool cond)
                {
                    if (cond) return false;
                    return true;
                }

                public bool CheckC(bool a, bool b)
                {
                    if (a && b) { return true; }
                    return false;
                }

                public bool CheckD(bool cond)
                {
                    if (cond) { return false; } else { return true; }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new BooleanReturnAnalyzer());
    }
}
