using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.NamedArgument;
using DemoriLabs.CodeAnalysis.NamedArgument;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NamedArgument;

[MemoryDiagnoser]
public class NamedArgumentCodeFixBenchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly NamedArgumentCodeFix _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<NamedArgumentAnalyzer>(
            """
            public class Renderer
            {
                public void Draw(int x, int y, int width, int height)
                {
                }

                public void Usage()
                {
                    Draw(10, 20, 100, 200);
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
