using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.StringEquals;
using DemoriLabs.CodeAnalysis.StringEquals;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.StringEquals;

[MemoryDiagnoser]
public class UseStringEqualsCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly UseStringEqualsCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<UseStringEqualsAnalyzer>(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s == "hello") { }
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
