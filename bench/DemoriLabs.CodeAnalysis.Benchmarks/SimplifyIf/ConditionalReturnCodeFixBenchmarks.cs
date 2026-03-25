using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class ConditionalReturnCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly ConditionalReturnCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<ConditionalReturnAnalyzer>(
            """
            public class C
            {
                public int M(bool cond)
                {
                    if (cond) return 1; else return 2;
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
