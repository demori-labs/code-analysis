using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.NamedArgument;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.NamedArgument;

[MemoryDiagnoser]
public class NamedArgumentAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Renderer
            {
                public void Draw(int x, int y, int width, int height)
                {
                }

                public void SetColor(bool enabled, bool visible, bool active)
                {
                }

                public void Usage()
                {
                    Draw(10, 20, 100, 200);
                    SetColor(true, false, true);

                    var x = 10;
                    var y = 20;
                    Draw(x, y, 100, 200);
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new NamedArgumentAnalyzer());
    }
}
