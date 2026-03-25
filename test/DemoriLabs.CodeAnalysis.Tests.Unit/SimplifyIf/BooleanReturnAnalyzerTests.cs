using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class BooleanReturnAnalyzerTests
{
    private static CSharpAnalyzerTest<BooleanReturnAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<BooleanReturnAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task WithElse_ReturnsTrueFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(bool cond)
                {
                    {|DL3008:if|} (cond)
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
    public async Task FallThrough_ReturnsTrueFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(bool cond)
                {
                    {|DL3008:if|} (cond)
                    {
                        return true;
                    }
                    return false;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WithElse_ReturnsFalseTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(bool cond)
                {
                    {|DL3008:if|} (cond)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FallThrough_ReturnsFalseTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(bool cond)
                {
                    {|DL3008:if|} (cond)
                    {
                        return false;
                    }
                    return true;
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
                public bool M(bool a, bool b)
                {
                    {|DL3008:if|} (a && b)
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
    public async Task MethodCallCondition_FallThrough_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(string s)
                {
                    {|DL3008:if|} (IsValid(s))
                    {
                        return true;
                    }
                    return false;
                }

                private bool IsValid(string s) => s.Length > 0;
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
                    Func<bool, bool> f = cond =>
                    {
                        {|DL3008:if|} (cond)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
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
                    bool Check(bool cond)
                    {
                        {|DL3008:if|} (cond)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsidePropertyGetter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int _value;

                public bool IsPositive
                {
                    get
                    {
                        {|DL3008:if|} (_value > 0)
                        {
                            return true;
                        }
                        return false;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonBooleanReturn_NoDiagnostic()
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
                    else
                    {
                        return 0;
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
                public bool M(bool cond)
                {
                    if (cond)
                    {
                        return true;
                    }
                    DoSomething();
                    return false;
                }

                private void DoSomething() { }
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
                public bool M(bool cond)
                {
                    if (cond)
                    {
                        return true;
                    }
                    throw new System.Exception();
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
                public bool M(bool cond)
                {
                    if (cond)
                    {
                        return true;
                    }
                    else
                    {
                        return true;
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
                public bool M(bool cond)
                {
                    if (cond)
                    {
                        return true;
                        ToString();
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
}
