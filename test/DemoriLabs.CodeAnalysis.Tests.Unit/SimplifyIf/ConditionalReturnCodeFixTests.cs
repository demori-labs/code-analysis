using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ConditionalReturnCodeFixTests
{
    private static CSharpCodeFixTest<ConditionalReturnAnalyzer, ConditionalReturnCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<ConditionalReturnAnalyzer, ConditionalReturnCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task FallThrough_SimplifiesToTernary()
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
            """,
            """
            public class C
            {
                public int M(bool cond)
                {
                    return cond ? 1 : 2;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WithElse_SimplifiesToTernary()
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
            """,
            """
            public class C
            {
                public int M(bool cond)
                {
                    return cond ? 1 : 2;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodCallValues_PreservedInFix()
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
            """,
            """
            public class C
            {
                public int M(bool cond)
                {
                    return cond ? Foo() : Bar();
                }

                private int Foo() => 1;
                private int Bar() => 2;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ComplexCondition_PreservedInFix()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool a, bool b)
                {
                    {|DL3010:if|} (a && b)
                    {
                        return 1;
                    }
                    else
                    {
                        return 2;
                    }
                }
            }
            """,
            """
            public class C
            {
                public int M(bool a, bool b)
                {
                    return a && b ? 1 : 2;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
