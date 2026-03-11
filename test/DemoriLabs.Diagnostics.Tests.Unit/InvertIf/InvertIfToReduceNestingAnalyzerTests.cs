using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.InvertIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.InvertIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class InvertIfToReduceNestingAnalyzerTests
{
    private static CSharpAnalyzerTest<InvertIfToReduceNestingAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<InvertIfToReduceNestingAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task VoidMethod_IfWrapsEntireBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VoidMethod_MultipleStatementsInBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                        DoMore();
                    }
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VoidMethod_StatementsBeforeIf_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    var x = 1;
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Constructor_IfWrapsBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int _value;

                public C(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        _value = 42;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PropertySetter_IfWrapsBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int _value;

                public int Value
                {
                    get => _value;
                    set
                    {
                        {|DL3002:if|} (value > 0)
                        {
                            _value = value;
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsyncTaskMethod_IfWrapsBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        await Task.Delay(1);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LocalFunction_IfWrapsBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    void Inner(bool condition)
                    {
                        {|DL3002:if|} (condition)
                        {
                            DoSomething();
                        }
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfWithElse_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                    {
                        DoSomething();
                    }
                    else
                    {
                        DoMore();
                    }
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfNotLastStatement_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                    {
                        DoSomething();
                    }

                    DoMore();
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonVoidMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    if (condition)
                    {
                        return 42;
                    }

                    return 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfWithoutBlock_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                        DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EmptyBlock_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                    {
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsyncTaskOfT_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task<int> M(bool condition)
                {
                    if (condition)
                    {
                        return await Task.FromResult(42);
                    }

                    return 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedIf_OnlyOuterFlagged()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    {|DL3002:if|} (a)
                    {
                        if (b)
                        {
                            DoSomething();
                        }
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsyncValueTaskMethod_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async ValueTask M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        await Task.Delay(1);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonVoidMethod_FollowedByReturn_MultipleStatements_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                        return 42;
                    }

                    return 0;
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonVoidMethod_FollowedByReturn_SingleStatement_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    if (condition)
                    {
                        return 42;
                    }

                    return 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonVoidMethod_FollowedByReturn_BodyWithoutReturn_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                        DoMore();
                    }

                    return 0;
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }
}
