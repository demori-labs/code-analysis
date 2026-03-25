using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class BooleanReturnCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly BooleanReturnCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<BooleanReturnAnalyzer>(
            """
            public class C
            {
                public bool M(bool cond)
                {
                    if (cond) return true; else return false;
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
