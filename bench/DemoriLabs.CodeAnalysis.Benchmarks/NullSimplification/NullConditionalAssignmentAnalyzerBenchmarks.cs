using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NullSimplification;

[MemoryDiagnoser]
public class NullConditionalAssignmentAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Target
            {
                public int Prop { get; set; }
                public int Count { get; set; }
                public void DoWork(int value) { }
            }

            public class Service
            {
                public void CheckA(Target? x)
                {
                    if (x is not null) x.Prop = 42;
                }

                public void CheckB(Target? x)
                {
                    if (x != null) x.Prop = 42;
                }

                public void CheckC(Target? x)
                {
                    if (x is not null) { x.Prop = 42; }
                }

                public void CheckD(Target? x)
                {
                    if (x is not null) x.DoWork(5);
                }

                public void CheckE(Target? x)
                {
                    if (x is not null) x.Count += 1;
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new NullConditionalAssignmentAnalyzer());
    }
}
