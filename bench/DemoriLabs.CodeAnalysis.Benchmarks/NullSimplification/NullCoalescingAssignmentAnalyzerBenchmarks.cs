using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NullSimplification;

[MemoryDiagnoser]
public class NullCoalescingAssignmentAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Foo { }

            public class Service
            {
                private Foo? _fieldA;
                private Foo? _fieldB;

                public void CheckA(Foo? x)
                {
                    if (x == null) x = new Foo();
                }

                public void CheckB(Foo? x)
                {
                    if (x is null) x = new Foo();
                }

                public void CheckC(Foo? x)
                {
                    if (x == null) { x = new Foo(); }
                }

                public void CheckD()
                {
                    if (_fieldA == null) _fieldA = new Foo();
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new NullCoalescingAssignmentAnalyzer());
    }
}
