using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class RedundantTypePatternAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            #nullable enable
            public class Animal { }
            public class Dog : Animal { }

            public class Service
            {
                public void Process(string str, string? nullableStr, int c, Dog dog)
                {
                    if (str is string)
                    {
                        System.Console.WriteLine(str);
                    }
                    if (nullableStr is string)
                    {
                        System.Console.WriteLine(nullableStr);
                    }
                    if (c is int)
                    {
                        System.Console.WriteLine(c);
                    }
                    if (dog is Animal)
                    {
                        System.Console.WriteLine(dog);
                    }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new RedundantTypePatternAnalyzer());
    }
}
