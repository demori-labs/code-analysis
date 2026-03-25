using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class ConditionalAssignmentCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly ConditionalAssignmentCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<ConditionalAssignmentAnalyzer>(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    if (cond) x = 1; else x = 2;
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
