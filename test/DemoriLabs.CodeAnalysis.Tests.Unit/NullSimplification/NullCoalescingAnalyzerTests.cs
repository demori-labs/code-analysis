using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NullSimplification;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NullCoalescingAnalyzerTests
{
    private static CSharpAnalyzerTest<NullCoalescingAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<NullCoalescingAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task Ternary_NotEqualsNull_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x != null {|DL3013:?|} x : y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Ternary_EqualsNull_Reversed_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x == null {|DL3013:?|} y : x;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Ternary_IsNotNull_Pattern_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x is not null {|DL3013:?|} x : y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Ternary_IsNull_Pattern_Reversed_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x is null {|DL3013:?|} y : x;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStatement_NotEqualsNull_FallThrough_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    {|DL3013:if|} (x != null)
                    {
                        return x;
                    }
                    return y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStatement_IsNotNull_Pattern_FallThrough_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    {|DL3013:if|} (x is not null)
                    {
                        return x;
                    }
                    return y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStatement_WithElse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    {|DL3013:if|} (x != null)
                    {
                        return x;
                    }
                    else
                    {
                        return y;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideLambda_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M()
                {
                    Func<string?, string, string> f = (x, y) =>
                    {
                        {|DL3013:if|} (x != null)
                        {
                            return x;
                        }
                        return y;
                    };
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodCall_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string y)
                {
                    return GetFoo() != null ? GetFoo() : y;
                }

                private string? GetFoo() => null;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PropertyAccess_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(C obj, string y)
                {
                    return obj.Prop != null ? obj.Prop : y;
                }

                public string? Prop { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DifferentVariables_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y, string z)
                {
                    return x != null ? y : z;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideExpressionTree_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq;
            using System.Linq.Expressions;

            public class C
            {
                public void M()
                {
                    Expression<Func<string?, string, string>> expr = (x, y) =>
                        x != null ? x : y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStatement_IntermediateStatements_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    if (x != null)
                    {
                        return x;
                    }
                    DoSomething();
                    return y;
                }

                private void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStatement_IsNull_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    if (x == null)
                    {
                        return x;
                    }
                    return y;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
