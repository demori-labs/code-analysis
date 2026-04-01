using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.EmptyString;
using DemoriLabs.CodeAnalysis.EmptyString;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.EmptyString;

[MemoryDiagnoser]
public class UseIsNullOrEmptyCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly UseIsNullOrEmptyCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<UseIsNullOrEmptyAnalyzer>(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s == "") { }
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
