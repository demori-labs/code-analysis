using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.RecordDesign;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.Analyzers;

[MemoryDiagnoser]
public class RecordsShouldNotHaveMutablePropertiesAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public record Person
            {
                public string Name { get; set; }
                public int Age { get; set; }
                public string Email { get; set; }
            }

            public record Address
            {
                public string Street { get; init; }
                public string City { get; init; }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new RecordsShouldNotHaveMutablePropertiesAnalyzer());
    }
}

[MemoryDiagnoser]
public class RecordsShouldNotHaveMutablePropertyTypesAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using System.Collections.Generic;

            public record Order
            {
                public List<string> Items { get; init; }
                public Dictionary<string, int> Quantities { get; init; }
                public string Id { get; init; }
            }

            public record ImmutableOrder
            {
                public IReadOnlyList<string> Items { get; init; }
                public string Id { get; init; }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new RecordsShouldNotHaveMutablePropertyTypesAnalyzer());
    }
}

[MemoryDiagnoser]
public class RecordPrimaryConstructorTooManyParametersAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public record SmallRecord(string A, int B);

            public record LargeRecord(string A, int B, bool C, double D, string E);

            public record MediumRecord(string A, int B, bool C);
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(
            _compilation,
            new RecordPrimaryConstructorTooManyParametersAnalyzer()
        );
    }
}

[MemoryDiagnoser]
public class DataClassCouldBeRecordAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Person
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }

            public class Service
            {
                public string Name { get; set; }
                public void DoWork() { }
            }

            public class Config
            {
                public string Key { get; init; }
                public string Value { get; init; }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new DataClassCouldBeRecordAnalyzer());
    }
}
