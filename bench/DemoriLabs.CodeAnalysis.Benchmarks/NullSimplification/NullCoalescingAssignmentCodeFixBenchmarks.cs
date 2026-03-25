using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.NullSimplification;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NullSimplification;

[MemoryDiagnoser]
public class NullCoalescingAssignmentCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly NullCoalescingAssignmentCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<NullCoalescingAssignmentAnalyzer>(
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    if (x == null) x = new Foo();
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
