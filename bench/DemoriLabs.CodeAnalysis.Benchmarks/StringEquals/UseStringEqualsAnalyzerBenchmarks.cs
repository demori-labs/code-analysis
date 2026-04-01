using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.StringEquals;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.StringEquals;

[MemoryDiagnoser]
public class UseStringEqualsAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public void Process(string name, string other)
                {
                    if (name == "admin") { }
                    if (name != "guest") { }
                    if ("root" == name) { }
                    if (name == other) { }
                    if (name != other) { }
                    if (name is "hello") { }
                    if (name is not "world") { }
                    if (name == null) { }
                    if (name is null) { }
                    if ("a" == "b") { }
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new UseStringEqualsAnalyzer());
    }
}
