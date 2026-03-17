using BenchmarkDotNet.Attributes;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.MultipleEnumeration;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.Diagnostics.Benchmarks.CodeFixes;

[MemoryDiagnoser]
public class MultipleEnumerationCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly MultipleEnumerationCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<MultipleEnumerationAnalyzer>(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class DataProcessor
            {
                public void Process(IEnumerable<int> numbers)
                {
                    var count = numbers.Count();
                    var sum = numbers.Sum();
                    Console.WriteLine($"Count: {count}, Sum: {sum}");
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
