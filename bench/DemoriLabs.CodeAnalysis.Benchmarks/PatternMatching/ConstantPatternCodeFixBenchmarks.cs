using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PatternMatching;

[MemoryDiagnoser]
public class ConstantPatternCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly ConstantPatternCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<ConstantPatternAnalyzer>(
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o == null) { }
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
