using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class TypeCheckAndCastCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly TypeCheckAndCastCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<TypeCheckAndCastAnalyzer>(
            """
            public class Animal { public string Name => ""; }

            public class C
            {
                public void M(object o)
                {
                    if (o is Animal)
                    {
                        var a = (Animal)o;
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
