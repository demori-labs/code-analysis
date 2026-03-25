using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class BooleanAssignmentAnalyzerTests
{
    private static CSharpAnalyzerTest<BooleanAssignmentAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<BooleanAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task Standard_TrueFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    bool x;
                    {|DL3009:if|} (cond)
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
    public async Task BlockBodies_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    bool x;
                    {|DL3009:if|} (cond)
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
    public async Task Negated_FalseTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool cond)
                {
                    bool x;
                    {|DL3009:if|} (cond)
                    {
                        x = false;
                    }
                    else
                    {
                        x = true;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ComplexCondition_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    bool x;
                    {|DL3009:if|} (a && b)
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
    public async Task PropertyAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool Flag { get; set; }

                public void M(bool cond)
                {
                    var obj = new C();
                    {|DL3009:if|} (cond)
                    {
                        obj.Flag = true;
                    }
                    else
                    {
                        obj.Flag = false;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FieldAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private bool _flag;

                public void M(bool cond)
                {
                    {|DL3009:if|} (cond)
                    {
                        _flag = true;
                    }
                    else
                    {
                        _flag = false;
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
                    bool x = false;
                    Action<bool> f = cond =>
                    {
                        {|DL3009:if|} (cond)
                        {
                            x = true;
                        }
                        else
                        {
                            x = false;
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
                    bool x = false;
                    void SetFlag(bool cond)
                    {
                        {|DL3009:if|} (cond)
                        {
                            x = true;
                        }
                        else
                        {
                            x = false;
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
                    bool x;
                    bool y;
                    if (cond)
                    {
                        x = true;
                    }
                    else
                    {
                        y = false;
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
                    bool x;
                    if (cond)
                    {
                        x = true;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonBoolean_NoDiagnostic()
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
                    else
                    {
                        x = 0;
                    }
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
                public void M(bool cond)
                {
                    bool x;
                    if (cond)
                    {
                        x = true;
                        ToString();
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
                    Expression<Func<int, bool>> expr = x =>
                        x > 0 ? true : false;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SameValue_NoDiagnostic()
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
                        x = true;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
