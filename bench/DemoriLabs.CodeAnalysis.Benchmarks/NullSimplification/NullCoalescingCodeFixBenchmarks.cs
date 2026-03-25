using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.NullSimplification;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NullSimplification;

[MemoryDiagnoser]
public class NullCoalescingCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly NullCoalescingCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<NullCoalescingAnalyzer>(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x != null ? x : y;
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
