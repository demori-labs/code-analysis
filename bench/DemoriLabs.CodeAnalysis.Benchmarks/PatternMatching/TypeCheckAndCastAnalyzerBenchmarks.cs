using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class TypeCheckAndCastAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Animal { public string Name => ""; }
            public class Dog : Animal { }

            public class Service
            {
                public void Process(object o)
                {
                    if (o is Animal)
                    {
                        var a = (Animal)o;
                        System.Console.WriteLine(a.Name);
                    }
                    if (o is Dog)
                    {
                        System.Console.WriteLine(((Dog)o).Name);
                    }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new TypeCheckAndCastAnalyzer());
    }
}
