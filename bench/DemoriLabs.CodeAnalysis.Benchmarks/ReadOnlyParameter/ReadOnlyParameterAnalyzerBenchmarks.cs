using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.ReadOnlyParameter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.ReadOnlyParameter;

[MemoryDiagnoser]
public class ReadOnlyParameterAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Calculator
            {
                public int Add([ReadOnly] int a, [ReadOnly] int b)
                {
                    a = 10;
                    return a + b;
                }

                public string Format([ReadOnly] string value)
                {
                    return value.ToUpper();
                }
            }
            """,
            MetadataReference.CreateFromFile(typeof(ReadOnlyAttribute).Assembly.Location)
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new ReadOnlyParameterAnalyzer());
    }
}

[MemoryDiagnoser]
public class SuggestReadOnlyPrimaryConstructorParameterAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Service(string name, int retryCount)
            {
                public string Name => name;
                public int RetryCount => retryCount;
            }

            public class ReadOnlyService([ReadOnly] string name)
            {
                public string Name => name;
            }
            """,
            MetadataReference.CreateFromFile(typeof(ReadOnlyAttribute).Assembly.Location)
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(
            _compilation,
            new SuggestReadOnlyPrimaryConstructorParameterAnalyzer()
        );
    }
}

[MemoryDiagnoser]
public class SuggestReadOnlyMethodParameterAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public void Process(int id, string name, bool active)
                {
                    System.Console.WriteLine(id);
                    System.Console.WriteLine(name);
                    System.Console.WriteLine(active);
                }

                public void Update(int id)
                {
                    id = 5;
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new SuggestReadOnlyMethodParameterAnalyzer());
    }
}
