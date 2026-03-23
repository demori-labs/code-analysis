using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CognitiveComplexity;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.CognitiveComplexity;

[MemoryDiagnoser]
public class CognitiveComplexityAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using System;
            using System.Collections.Generic;

            public class ComplexService
            {
                public string Process(List<string> items, bool validate, int maxRetries)
                {
                    if (items == null)
                        throw new ArgumentNullException(nameof(items));

                    for (int attempt = 0; attempt < maxRetries; attempt++)
                    {
                        try
                        {
                            foreach (var item in items)
                            {
                                if (validate)
                                {
                                    if (string.IsNullOrEmpty(item))
                                        continue;

                                    if (item.Length > 100)
                                    {
                                        while (item.Contains("  "))
                                        {
                                            if (attempt > 2)
                                                return "too many retries";
                                        }
                                    }
                                }
                            }

                            return "success";
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("retry"))
                        {
                            if (attempt == maxRetries - 1)
                                throw;
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }

                    return "failed";
                }

                public int SimpleMethod(int x)
                {
                    if (x > 0)
                        return x;
                    return -x;
                }

                public int Fibonacci(int n)
                {
                    if (n <= 1)
                        return n;
                    return Fibonacci(n - 1) + Fibonacci(n - 2);
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new CognitiveComplexityAnalyzer());
    }
}
