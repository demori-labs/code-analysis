using BenchmarkDotNet.Attributes;
using DemoriLabs.Diagnostics.MultipleEnumeration;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.Diagnostics.Benchmarks.Analyzers;

[MemoryDiagnoser]
public class MultipleEnumerationAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
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

                public void SafeProcess(IReadOnlyList<int> numbers)
                {
                    var count = numbers.Count;
                    var first = numbers[0];
                }

                public void SingleUse(IEnumerable<string> items)
                {
                    foreach (var item in items)
                    {
                        Console.WriteLine(item);
                    }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new MultipleEnumerationAnalyzer());
    }
}
