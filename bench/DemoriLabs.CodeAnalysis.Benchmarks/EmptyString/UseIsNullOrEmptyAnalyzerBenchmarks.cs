using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.EmptyString;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.EmptyString;

[MemoryDiagnoser]
public class UseIsNullOrEmptyAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using System;

            public class Service
            {
                public void Process(string name, string? nullable)
                {
                    if (name == "") { }
                    if (name != "") { }
                    if ("" == name) { }
                    if (name == string.Empty) { }
                    if (name.Length == 0) { }
                    if (name.Length is 0) { }
                    if (nullable?.Length == 0) { }
                    if (name is "") { }
                    if (name is not "") { }
                    if (nullable is null or "") { }
                    if (nullable is not null and not "") { }
                    if (string.Equals(name, "")) { }
                    if (string.Equals(name, "", StringComparison.Ordinal)) { }
                    if (name.Equals("")) { }
                    if (name == "hello") { }
                    if (name == null) { }
                    if (name.Length == 5) { }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new UseIsNullOrEmptyAnalyzer());
    }
}
