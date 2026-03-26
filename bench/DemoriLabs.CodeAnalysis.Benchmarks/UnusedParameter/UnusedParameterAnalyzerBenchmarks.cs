using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.UnusedParameter;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.UnusedParameter;

[MemoryDiagnoser]
public class UnusedParameterAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using System;
            using System.Collections.Generic;

            public interface IService
            {
                void Execute(int command);
                void Process(string data, int flags);
            }

            public abstract class BaseHandler
            {
                public abstract void Handle(int code);
                public virtual void OnEvent(int id) { }
            }

            public class ServiceImpl : IService, BaseHandler
            {
                // No diagnostic: interface implementation
                public void Execute(int command)
                {
                    Console.WriteLine("Executing");
                }

                // No diagnostic: interface implementation
                public void Process(string data, int flags)
                {
                    Console.WriteLine("Processing");
                }

                // No diagnostic: override
                public override void Handle(int code)
                {
                    Console.WriteLine("Handling");
                }

                // No diagnostic: override
                public override void OnEvent(int id)
                {
                    Console.WriteLine("Event");
                }

                // Should detect: unused parameter
                public void Compute(int value, string label)
                {
                    Console.WriteLine(value * 2);
                }

                // Should detect: all params unused
                public void LogMetrics(int count, double rate, string tag)
                {
                    Console.WriteLine("Logging metrics");
                }

                // Should detect: unused in expression body
                public int Transform(int input, int scale) => input * 2;

                // No diagnostic: all params used
                public void Report(string title, int year, bool verbose)
                {
                    Console.WriteLine($"{title} ({year})");
                    if (verbose)
                    {
                        Console.WriteLine("Detailed report");
                    }
                }

                // No diagnostic: event handler signature
                public void OnClick(object sender, EventArgs e)
                {
                    Console.WriteLine("Clicked");
                }

                // No diagnostic: discard parameter
                public void Callback(int _, string _prefix)
                {
                    Console.WriteLine("Callback");
                }

                // No diagnostic: out parameter
                public bool TryParse(string input, out int result)
                {
                    result = 0;
                    return int.TryParse(input, out result);
                }

                // No diagnostic: used in lambda
                public void Deferred(int x)
                {
                    Action a = () => Console.WriteLine(x);
                    a();
                }

                // No diagnostic: used in local function
                public void WithHelper(int x)
                {
                    void Inner()
                    {
                        Console.WriteLine(x);
                    }
                    Inner();
                }

                // Should detect: constructor with unused param
                public ServiceImpl(int initialCapacity)
                {
                    Console.WriteLine("Initialised");
                }

                // No diagnostic: no parameters
                public void NoParams()
                {
                    Console.WriteLine("No params");
                }

                // No diagnostic: all used in arithmetic
                public double Calculate(double a, double b, double c)
                {
                    return a * b + c;
                }

                // No diagnostic: virtual method
                public virtual void Extensible(int x)
                {
                    Console.WriteLine("Base");
                }

                // Should detect: static method with unused param
                public static void Utility(string name, int unused)
                {
                    Console.WriteLine(name);
                }
            }

            public static class Extensions
            {
                // Should detect: second param unused (this param excluded)
                public static void Format(this string s, int precision)
                {
                    Console.WriteLine(s);
                }

                // No diagnostic: both used
                public static string Wrap(this string s, string wrapper)
                {
                    return wrapper + s + wrapper;
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new UnusedParameterAnalyzer());
    }
}
