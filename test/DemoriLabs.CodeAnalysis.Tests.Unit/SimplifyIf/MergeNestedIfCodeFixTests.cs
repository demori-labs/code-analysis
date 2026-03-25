using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class MergeNestedIfCodeFixTests
{
    private static CSharpCodeFixTest<MergeNestedIfAnalyzer, MergeNestedIfCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<MergeNestedIfAnalyzer, MergeNestedIfCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task SimpleMerge_CombinesConditions()
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
            """,
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a && b)
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

    [Test]
    public async Task OuterConditionNeedsParentheses_WrapsInParentheses()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    {|DL3012:if|} (a || b)
                    {
                        if (c)
                        {
                            DoStuff();
                        }
                    }
                }

                private void DoStuff() { }
            }
            """,
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    if ((a || b) && c)
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

    [Test]
    public async Task InnerConditionNeedsParentheses_WrapsInParentheses()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    {|DL3012:if|} (a)
                    {
                        if (b || c)
                        {
                            DoStuff();
                        }
                    }
                }

                private void DoStuff() { }
            }
            """,
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    if (a && (b || c))
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

    [Test]
    public async Task MultipleStatementsInBody_PreservesBody()
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
            """,
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a && b)
                    {
                        DoStuff();
                        DoMoreStuff();
                    }
                }

                private void DoStuff() { }
                private void DoMoreStuff() { }
            }
            """
        );

        await test.RunAsync();
    }
}
