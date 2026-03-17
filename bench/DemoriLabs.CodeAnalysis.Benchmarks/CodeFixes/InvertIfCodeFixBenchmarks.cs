using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes;
using DemoriLabs.CodeAnalysis.InvertIf;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.CodeFixes;

[MemoryDiagnoser]
public class InvertIfToReduceNestingCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly InvertIfToReduceNestingCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<InvertIfToReduceNestingAnalyzer>(
            """
            using System;

            public class Guard
            {
                public void ProcessItem(string item)
                {
                    if (item != null)
                    {
                        if (item.Length > 0)
                        {
                            Console.WriteLine(item);
                            Console.WriteLine(item.ToUpper());
                        }
                    }
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
