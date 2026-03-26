using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.UnusedParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.UnusedParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UnusedParameterAnalyzerTests
{
    private static CSharpAnalyzerTest<UnusedParameterAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<UnusedParameterAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task MethodWithUnusedParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2005:x|})
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleParams_OneUnused_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(int x, int {|DL2005:y|})
                {
                    Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleParams_AllUnused_ReportsMultipleDiagnostics()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2005:x|}, int {|DL2005:y|})
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorWithUnusedParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public C(int {|DL2005:x|})
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LocalFunctionWithUnusedParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    void Inner(int {|DL2005:x|})
                    {
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StaticMethodWithUnusedParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public static void M(int {|DL2005:x|})
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExpressionBodyMethodUnusedParam_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(int {|DL2005:x|}) => Console.WriteLine(42);
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionMethodSecondParamUnused_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public static class Extensions
            {
                public static void M(this string s, int {|DL2005:x|}) => Console.WriteLine(s);
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AllParametersUsed_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(int x)
                {
                    Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParameterUsedInExpressionBody_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(int x) => x + 1;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InterfaceImplementation_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public interface I
            {
                void M(int x);
            }

            public class C : I
            {
                public void M(int x)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExplicitInterfaceImplementation_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public interface I
            {
                void M(int x);
            }

            public class C : I
            {
                void I.M(int x)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OverrideMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Base
            {
                public virtual void M(int x)
                {
                }
            }

            public class Derived : Base
            {
                public override void M(int x)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VirtualMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public virtual void M(int x)
                {
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
    public async Task ExternMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Runtime.InteropServices;

            public class C
            {
                [DllImport("kernel32.dll")]
                public static extern void M(int x);
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DiscardParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int _)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task UnderscorePrefixParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int _unused)
                {
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
                    x = 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EventHandlerSignature_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void OnClick(object sender, EventArgs e)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EntryPointMain_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Program
            {
                public static void Main(string[] args)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionMethodThisParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Extensions
            {
                public static void M(this string s)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParameterUsedInNestedLambda_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(int x)
                {
                    Action a = () => Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParameterUsedInLocalFunction_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(int x)
                {
                    void Inner()
                    {
                        Console.WriteLine(x);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParameterUsedInNameof_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(int x)
                {
                    var s = nameof(x);
                    Console.WriteLine(s);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParameterPassedByRef_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    Foo(ref x);
                }

                private void Foo(ref int y)
                {
                    y = 42;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoParameters_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PartialMethodWithoutBody_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public partial class C
            {
                partial void M(int x);
            }
            """
        );

        await test.RunAsync();
    }
}
