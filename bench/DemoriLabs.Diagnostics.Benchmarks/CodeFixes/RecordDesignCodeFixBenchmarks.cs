using BenchmarkDotNet.Attributes;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.RecordDesign;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.Diagnostics.Benchmarks.CodeFixes;

[MemoryDiagnoser]
public class RecordsShouldNotHaveMutablePropertiesCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly RecordsShouldNotHaveMutablePropertiesCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<RecordsShouldNotHaveMutablePropertiesAnalyzer>(
            """
            public record Person
            {
                public string Name { get; set; }
                public int Age { get; set; }
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
public class RecordPrimaryConstructorTooManyParametersCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly RecordPrimaryConstructorTooManyParametersCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<RecordPrimaryConstructorTooManyParametersAnalyzer>(
            """
            public record LargeRecord(string Name, int Age, bool Active, double Score, string Email);
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
public class DataClassCouldBeRecordCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly DataClassCouldBeRecordCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<DataClassCouldBeRecordAnalyzer>(
            """
            public class Person
            {
                public string Name { get; set; }
                public int Age { get; set; }
                public string Email { get; set; }
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
