using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.InvertIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.InvertIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class InvertIfToReduceNestingCodeFixTests
{
    private static CSharpCodeFixTest<
        InvertIfToReduceNestingAnalyzer,
        InvertIfToReduceNestingCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<InvertIfToReduceNestingAnalyzer, InvertIfToReduceNestingCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task SimpleCondition_InvertsWithNot()
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
            """,
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (!condition)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedCondition_RemovesNegation()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    {|DL3002:if|} (!condition)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualityComparison_FlipsToNotEqual()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    {|DL3002:if|} (x == 0)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x != 0)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualComparison_FlipsToEqual()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    {|DL3002:if|} (x != 0)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x == 0)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GreaterThan_FlipsToLessThanOrEqual()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    {|DL3002:if|} (x > 0)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x <= 0)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNull_InvertsToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    {|DL3002:if|} (s is null)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(string? s)
                {
                    if (s is not null)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNull_InvertsToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    {|DL3002:if|} (s is not null)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(string? s)
                {
                    if (s is null)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleStatements_AllExtracted()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoFirst();
                        DoSecond();
                        DoThird();
                    }
                }

                private static void DoFirst() { }
                private static void DoSecond() { }
                private static void DoThird() { }
            }
            """,
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (!condition)
                        return;

                    DoFirst();
                    DoSecond();
                    DoThird();
                }

                private static void DoFirst() { }
                private static void DoSecond() { }
                private static void DoThird() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StatementsBeforeIf_PreservedInOutput()
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
            """,
            """
            public class C
            {
                public void M(bool condition)
                {
                    var x = 1;
                    if (!condition)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TrueLiteral_InvertsToFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    {|DL3002:if|} (true)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                    if (false)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LogicalAnd_WrapsWithNot()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    {|DL3002:if|} (a && b)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (!(a && b))
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonVoidMethod_InvertsWithReturnValue()
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
            """,
            """
            public class C
            {
                public int M(bool condition)
                {
                    if (!condition)
                        return 0;

                    DoSomething();
                    return 42;
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonVoidMethod_BodyWithoutReturn_KeepsTrailingReturn()
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
            """,
            """
            public class C
            {
                public int M(bool condition)
                {
                    if (!condition)
                        return 0;

                    DoSomething();
                    DoMore();
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
