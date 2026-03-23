using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.PrimaryConstructor;
using DemoriLabs.CodeAnalysis.PrimaryConstructor;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PrimaryConstructor;

[MemoryDiagnoser]
public class UsePrimaryConstructorCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly UsePrimaryConstructorCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<UsePrimaryConstructorAnalyzer>(
            """
            public class MyService
            {
                private readonly int _id;
                private readonly string _name;

                public MyService(int id, string name)
                {
                    _id = id;
                    _name = name;
                }

                public int GetId() => _id;
                public string GetName() => _name;
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
