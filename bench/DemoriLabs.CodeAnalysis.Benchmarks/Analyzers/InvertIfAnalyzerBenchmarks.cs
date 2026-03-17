using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.InvertIf;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.Analyzers;

[MemoryDiagnoser]
public class InvertIfToReduceNestingAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using System;
            using System.Collections.Generic;

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

                public int Calculate(int? value)
                {
                    if (value.HasValue)
                    {
                        var result = value.Value * 2;
                        return result + 1;
                    }

                    return 0;
                }

                public void AlreadyFlat(string input)
                {
                    if (string.IsNullOrEmpty(input))
                        return;

                    Console.WriteLine(input);
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new InvertIfToReduceNestingAnalyzer());
    }
}
