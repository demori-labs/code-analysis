using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ConditionalReturnAnalyzerTests
{
    private static CSharpAnalyzerTest<ConditionalReturnAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<ConditionalReturnAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task FallThrough_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond)
                {
                    {|DL3010:if|} (cond)
                    {
                        return 1;
                    }
                    return 2;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WithElse_NoBraces_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond)
                {
                    {|DL3010:if|} (cond)
                    {
                        return 1;
                    }
                    else
                    {
                        return 2;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WithElse_BlockBodies_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond)
                {
                    {|DL3010:if|} (cond)
                    {
                        return 1;
                    }
                    else
                    {
                        return 2;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodCallReturnValues_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond)
                {
                    {|DL3010:if|} (cond)
                    {
                        return Foo();
                    }
                    else
                    {
                        return Bar();
                    }
                }

                private int Foo() => 1;
                private int Bar() => 2;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ShortNewExpressions_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int[] M(bool cond)
                {
                    {|DL3010:if|} (cond)
                    {
                        return new int[] { 1 };
                    }
                    else
                    {
                        return new int[] { 2 };
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
                    Func<bool, int> f = cond =>
                    {
                        {|DL3010:if|} (cond)
                        {
                            return 1;
                        }
                        else
                        {
                            return 2;
                        }
                    };
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideLocalFunction_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    int Pick(bool cond)
                    {
                        {|DL3010:if|} (cond)
                        {
                            return 1;
                        }
                        else
                        {
                            return 2;
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VoidReturn_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    if (cond)
                    {
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BooleanLiterals_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(bool cond)
                {
                    if (cond)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedTernary_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond, bool x)
                {
                    if (cond)
                    {
                        return x ? 1 : 2;
                    }
                    else
                    {
                        return 3;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IntermediateStatement_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond)
                {
                    if (cond)
                    {
                        return 1;
                    }
                    DoStuff();
                    return 2;
                }

                private void DoStuff() { }
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
                    Expression<Func<int, int>> expr = x =>
                        x > 0 ? 1 : 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoElseNoFallThrough_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond)
                {
                    if (cond)
                    {
                        return 1;
                    }
                    throw new System.Exception();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleStatementsInBody_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool cond)
                {
                    if (cond)
                    {
                        DoStuff();
                        return 1;
                    }
                    return 2;
                }

                private void DoStuff() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LongReturnValue_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(bool cond)
                {
                    if (cond)
                    {
                        return "This is a very long string value that exceeds sixty characters limit here";
                    }
                    else
                    {
                        return "short";
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
