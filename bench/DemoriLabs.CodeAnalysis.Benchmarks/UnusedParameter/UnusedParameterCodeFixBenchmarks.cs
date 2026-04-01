using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.UnusedParameter;
using DemoriLabs.CodeAnalysis.UnusedParameter;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.UnusedParameter;

[MemoryDiagnoser]
public class UnusedParameterCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly UnusedParameterCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<UnusedParameterAnalyzer>(
            """
            public class Service
            {
                public void Process(int id, int unused)
                {
                    System.Console.WriteLine(id);
                }

                public void Caller()
                {
                    Process(1, 2);
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
