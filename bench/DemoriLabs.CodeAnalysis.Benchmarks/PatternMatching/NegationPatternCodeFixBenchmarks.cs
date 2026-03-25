using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class NegationPatternCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly NegationPatternCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<NegationPatternAnalyzer>(
            """
            public class C
            {
                public void M(bool flag)
                {
                    if (!flag) { }
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
