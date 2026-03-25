using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.NullSimplification;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NullSimplification;

[MemoryDiagnoser]
public class NullConditionalAssignmentCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly NullConditionalAssignmentCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<NullConditionalAssignmentAnalyzer>(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    if (x is not null) x.Prop = 42;
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
