using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class MergeNestedIfCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly MergeNestedIfCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<MergeNestedIfAnalyzer>(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                            DoStuff();
                        }
                    }
                }

                private void DoStuff() { }
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
