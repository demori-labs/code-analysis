using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.ReadOnlyParameter;
using DemoriLabs.CodeAnalysis.ReadOnlyParameter;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.ReadOnlyParameter;

[MemoryDiagnoser]
public class ReadOnlyParameterCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly ReadOnlyParameterCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<ReadOnlyParameterAnalyzer>(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Calculator
            {
                public int Add([ReadOnly] int a, int b)
                {
                    a = 10;
                    return a + b;
                }
            }
            """,
            MetadataReference.CreateFromFile(typeof(ReadOnlyAttribute).Assembly.Location)
        );
    }

    [Benchmark]
    public async Task<Solution> ApplyFix()
    {
        return await _runner.ApplyFixAsync(_codeFix);
    }
}

[MemoryDiagnoser]
public class SuggestReadOnlyPrimaryConstructorParameterCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly SuggestReadOnlyPrimaryConstructorParameterCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<SuggestReadOnlyPrimaryConstructorParameterAnalyzer>(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Service(string name)
            {
                public string Name => name;
            }
            """,
            MetadataReference.CreateFromFile(typeof(ReadOnlyAttribute).Assembly.Location)
        );
    }

    [Benchmark]
    public async Task<Solution> ApplyFix()
    {
        return await _runner.ApplyFixAsync(_codeFix);
    }
}

[MemoryDiagnoser]
public class SuggestReadOnlyMethodParameterCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly SuggestReadOnlyMethodParameterCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<SuggestReadOnlyMethodParameterAnalyzer>(
            """
            public class Service
            {
                public void Process(int id)
                {
                    System.Console.WriteLine(id);
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

[MemoryDiagnoser]
public class ReadOnlyIncompatibleModifierCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly ReadOnlyIncompatibleModifierCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<ReadOnlyParameterAnalyzer>(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Service
            {
                public void Process([ReadOnly] ref int value)
                {
                }
            }
            """,
            MetadataReference.CreateFromFile(typeof(ReadOnlyAttribute).Assembly.Location)
        );
    }

    [Benchmark]
    public async Task<Solution> ApplyFix()
    {
        return await _runner.ApplyFixAsync(_codeFix);
    }
}
