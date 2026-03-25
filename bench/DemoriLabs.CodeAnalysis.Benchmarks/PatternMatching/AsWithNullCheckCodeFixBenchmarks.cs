using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class AsWithNullCheckCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly AsWithNullCheckCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<AsWithNullCheckAnalyzer>(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o as Animal;
                    if (a != null)
                    {
                        System.Console.WriteLine(a);
                    }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task<Solution> ApplyFix()
    {
        return await _runner.ApplyFixAsync(_codeFix);
    }
}
