using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.PrimaryConstructor;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.PrimaryConstructor;

[MemoryDiagnoser]
public class UsePrimaryConstructorAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class ServiceA
            {
                private readonly int _id;
                private readonly string _name;

                public ServiceA(int id, string name)
                {
                    _id = id;
                    _name = name;
                }

                public int GetId() => _id;
            }

            public class ServiceB
            {
                private readonly string _value;

                public ServiceB(string value)
                {
                    _value = value;
                    Initialize();
                }

                private void Initialize() { }
            }

            public class ServiceC(int id)
            {
                public int Id => id;
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new UsePrimaryConstructorAnalyzer());
    }
}
