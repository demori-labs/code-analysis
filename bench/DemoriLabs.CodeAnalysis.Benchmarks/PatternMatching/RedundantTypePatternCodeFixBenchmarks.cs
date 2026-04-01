using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class RedundantTypePatternCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly RedundantTypePatternCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<RedundantTypePatternAnalyzer>(
            """
            #nullable enable
            public class C
            {
                public void M(string? str)
                {
                    if (str is string)
                    {
                        System.Console.WriteLine(str);
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
