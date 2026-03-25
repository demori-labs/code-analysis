using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class MergeNestedIfAnalyzerTests
{
    private static CSharpAnalyzerTest<MergeNestedIfAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<MergeNestedIfAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task SimpleMerge_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    {|DL3012:if|} (a)
                    {
                        if (b)
                        {
                            DoStuff();
                        }
                    }
                }

                private void DoStuff() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultilineBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    {|DL3012:if|} (a)
                    {
                        if (b)
                        {
                            DoStuff();
                            DoMoreStuff();
                        }
                    }
                }

                private void DoStuff() { }
                private void DoMoreStuff() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OuterConditionAlreadyAnd_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    {|DL3012:if|} (a && c)
                    {
                        if (b)
                        {
                            DoStuff();
                        }
                    }
                }

                private void DoStuff() { }
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
                    Action<bool, bool> f = (a, b) =>
                    {
                        {|DL3012:if|} (a)
                        {
                            if (b)
                            {
                                Console.WriteLine();
                            }
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
                    void Inner(bool a, bool b)
                    {
                        {|DL3012:if|} (a)
                        {
                            if (b)
                            {
                                System.Console.WriteLine();
                            }
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DeeplyNested_FlagsOuterPairOnly()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    {|DL3012:if|} (a)
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

        await test.RunAsync();
    }

    [Test]
    public async Task OuterIfHasElse_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                            DoStuff();
                        }
                    }
                    else
                    {
                        DoOther();
                    }
                }

                private void DoStuff() { }
                private void DoOther() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InnerIfHasElse_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                            DoStuff();
                        }
                        else
                        {
                            DoOther();
                        }
                    }
                }

                private void DoStuff() { }
                private void DoOther() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OuterBlockHasStatementBeforeInnerIf_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                    {
                        DoStuff();
                        if (b)
                        {
                            DoOther();
                        }
                    }
                }

                private void DoStuff() { }
                private void DoOther() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OuterBlockHasStatementAfterInnerIf_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                    {
                        if (b)
                        {
                            DoOther();
                        }
                        DoStuff();
                    }
                }

                private void DoStuff() { }
                private void DoOther() { }
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
                        x > 0 ? x : -x;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InnerStatementIsNotIf_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a)
                {
                    if (a)
                    {
                        DoStuff();
                    }
                }

                private void DoStuff() { }
            }
            """
        );

        await test.RunAsync();
    }
}
