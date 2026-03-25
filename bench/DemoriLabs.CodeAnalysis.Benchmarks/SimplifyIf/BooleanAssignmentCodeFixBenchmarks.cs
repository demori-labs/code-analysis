using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class BooleanAssignmentCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly BooleanAssignmentCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<BooleanAssignmentAnalyzer>(
            """
            public class C
            {
                public void M(bool cond)
                {
                    bool x;
                    if (cond) x = true; else x = false;
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
