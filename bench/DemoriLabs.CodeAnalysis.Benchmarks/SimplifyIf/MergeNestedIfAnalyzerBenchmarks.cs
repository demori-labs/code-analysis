using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.SimplifyIf;

[MemoryDiagnoser]
public class MergeNestedIfAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            public class Service
            {
                public void CheckA(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                            DoStuff();
                        }
                    }
                }

                public void CheckB(bool a, bool b, bool c)
                {
                    if (a || b)
                    {
                        if (c)
                        {
                            DoStuff();
                        }
                    }
                }

                public void CheckC(bool a, bool b, bool c)
                {
                    if (a)
                    {
                        if (b || c)
                        {
                            DoStuff();
                        }
                    }
                }

                public void CheckD(bool a, bool b, bool c)
                {
                    if (a)
                    {
                        if (b)
                        {
                            if (c)
                            {
                                DoStuff();
                            }
                        }
                    }
                }

                private void DoStuff() { }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new MergeNestedIfAnalyzer());
    }
}
