using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.ReadOnlyParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.ReadOnlyParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class SuggestReadOnlyMethodParameterAnalyzerTests
{
    private static CSharpAnalyzerTest<SuggestReadOnlyMethodParameterAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        var test = new CSharpAnalyzerTest<SuggestReadOnlyMethodParameterAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task UnreassignedParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2004:x|})
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleParams_AllFlagged()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2004:x|}, int {|DL2004:y|})
                {
                    System.Console.WriteLine(x);
                    y = 10;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VirtualMethod_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public virtual void M(int {|DL2004:x|})
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OverrideMethod_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Base
            {
                public virtual void M(int {|DL2004:x|}) { }
            }

            public class Derived : Base
            {
                public override void M(int {|DL2004:x|})
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private readonly int _x;

                public C(int {|DL2004:x|})
                {
                    _x = x;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LocalFunctionParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    void Inner(int {|DL2004:x|})
                    {
                        System.Console.WriteLine(x);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReassignedParameter_StillReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2004:x|})
                {
                    x = 5;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IncrementedParameter_StillReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2004:x|})
                {
                    x++;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CompoundAssignment_StillReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2004:x|})
                {
                    x += 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AlreadyReadOnly_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MutableAttribute_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([Mutable] int x)
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RefParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(ref int x)
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OutParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(out int x)
                {
                    x = 5;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(in int x)
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task UnderscorePrefix_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int _x)
                {
                    System.Console.WriteLine(_x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AbstractMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public abstract class C
            {
                public abstract void M(int x);
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InterfaceMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public interface IC
            {
                void M(int x);
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EventHandler_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void OnClick(object sender, EventArgs e)
                {
                    System.Console.WriteLine(sender);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EntryPoint_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Program
            {
                public static void Main(string[] args)
                {
                    System.Console.WriteLine(args.Length);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionMethodThisParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Extensions
            {
                public static void Print(this string {|DL2004:s|})
                {
                    System.Console.WriteLine(s);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionMethodThisWithMutable_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public static class Extensions
            {
                public static void Print([Mutable] this string s)
                {
                    System.Console.WriteLine(s);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_MethodParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Extensions
            {
                extension(string {|DL2004:source|})
                {
                    public string Repeat(int {|DL2004:count|})
                    {
                        return string.Concat(System.Linq.Enumerable.Repeat(source, count));
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_ReceiverParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Extensions
            {
                extension(string {|DL2004:source|})
                {
                    public bool IsEmpty => source.Length == 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_StaticReceiverNoName_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public static class Extensions
            {
                extension(IEnumerable<int>)
                {
                    public static IEnumerable<int> Empty => System.Linq.Enumerable.Empty<int>();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_StaticNoReceiver_MethodParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public static class Extensions
            {
                extension(IEnumerable<int>)
                {
                    public static IEnumerable<int> Combine(IEnumerable<int> {|DL2004:first|}, IEnumerable<int> {|DL2004:second|})
                    {
                        foreach (var item in first)
                            yield return item;
                        foreach (var item in second)
                            yield return item;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_GenericReceiver_MethodParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public static class Extensions
            {
                extension<T>(IEnumerable<T> {|DL2004:source|})
                {
                    public IEnumerable<T> Take(int {|DL2004:count|})
                    {
                        return source.Take(count);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_ReassignedMethodParameter_StillReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Extensions
            {
                extension(string {|DL2004:source|})
                {
                    public string RepeatOrDefault(int {|DL2004:count|})
                    {
                        if (count < 0)
                            count = 1;
                        return string.Concat(System.Linq.Enumerable.Repeat(source, count));
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
