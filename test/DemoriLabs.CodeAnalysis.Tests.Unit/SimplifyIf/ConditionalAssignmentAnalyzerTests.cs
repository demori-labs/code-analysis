using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ConditionalAssignmentAnalyzerTests
{
    private static CSharpAnalyzerTest<ConditionalAssignmentAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<ConditionalAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task Standard_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    {|DL3011:if|} (cond)
                    {
                        x = 1;
                    }
                    else
                    {
                        x = 2;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockBodies_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    {|DL3011:if|} (cond)
                    {
                        x = 1;
                    }
                    else
                    {
                        x = 2;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodCallValues_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    {|DL3011:if|} (cond)
                    {
                        x = Foo();
                    }
                    else
                    {
                        x = Bar();
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
    public async Task ShortExpressionValues_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond, int a, int b)
                {
                    int x;
                    {|DL3011:if|} (cond)
                    {
                        x = a + 1;
                    }
                    else
                    {
                        x = b + 1;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PropertyAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Value { get; set; }

                public void M(bool cond, int a, int b)
                {
                    var obj = new C();
                    {|DL3011:if|} (cond)
                    {
                        obj.Value = a;
                    }
                    else
                    {
                        obj.Value = b;
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
                    int x = 0;
                    Action<bool> f = cond =>
                    {
                        {|DL3011:if|} (cond)
                        {
                            x = 1;
                        }
                        else
                        {
                            x = 2;
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
                    int x = 0;
                    void SetValue(bool cond)
                    {
                        {|DL3011:if|} (cond)
                        {
                            x = 1;
                        }
                        else
                        {
                            x = 2;
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DifferentTargets_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    int y;
                    if (cond)
                    {
                        x = 1;
                    }
                    else
                    {
                        y = 2;
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
                public void M(bool cond)
                {
                    bool x;
                    if (cond)
                    {
                        x = true;
                    }
                    else
                    {
                        x = false;
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
                public void M(bool cond, bool y)
                {
                    int x;
                    if (cond)
                    {
                        x = y ? 1 : 2;
                    }
                    else
                    {
                        x = 3;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LongValue_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    string x;
                    if (cond)
                    {
                        x = "This is a very long string value that exceeds sixty characters limit here";
                    }
                    else
                    {
                        x = "short";
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoElse_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    if (cond)
                    {
                        x = 1;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleStatements_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    if (cond)
                    {
                        x = 1;
                        ToString();
                    }
                    else
                    {
                        x = 2;
                    }
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
                    Expression<Func<int, int>> expr = x =>
                        x > 0 ? 1 : 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CompoundAssignment_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x = 0;
                    if (cond)
                    {
                        x += 1;
                    }
                    else
                    {
                        x += 2;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
