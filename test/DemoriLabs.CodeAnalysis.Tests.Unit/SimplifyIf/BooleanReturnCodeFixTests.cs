using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class BooleanReturnCodeFixTests
{
    private static CSharpCodeFixTest<BooleanReturnAnalyzer, BooleanReturnCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<BooleanReturnAnalyzer, BooleanReturnCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task WithElse_ReturnsTrueFalse_SimplifiesToReturnCondition()
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
            """,
            """
            public class C
            {
                public bool M(bool cond)
                {
                    return cond;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FallThrough_ReturnsTrueFalse_SimplifiesToReturnCondition()
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
            """,
            """
            public class C
            {
                public bool M(bool cond)
                {
                    return cond;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WithElse_ReturnsFalseTrue_SimplifiesToReturnConditionIsFalse()
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
            """,
            """
            public class C
            {
                public bool M(bool cond)
                {
                    return cond is false;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FallThrough_ReturnsFalseTrue_SimplifiesToReturnConditionIsFalse()
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
            """,
            """
            public class C
            {
                public bool M(bool cond)
                {
                    return cond is false;
                }
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
            """,
            """
            public class C
            {
                public bool M(bool a, bool b)
                {
                    return a && b;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
