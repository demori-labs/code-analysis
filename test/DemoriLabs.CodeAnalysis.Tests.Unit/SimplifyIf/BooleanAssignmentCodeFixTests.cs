using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class BooleanAssignmentCodeFixTests
{
    private static CSharpCodeFixTest<BooleanAssignmentAnalyzer, BooleanAssignmentCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<BooleanAssignmentAnalyzer, BooleanAssignmentCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task Standard_TrueFalse_SimplifiesToAssignment()
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
            """,
            """
            public class C
            {
                public void M(bool cond)
                {
                    bool x;
                    x = cond;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Negated_FalseTrue_SimplifiesToAssignmentIsFalse()
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
            """,
            """
            public class C
            {
                public void M(bool cond)
                {
                    bool x;
                    x = cond is false;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockBodies_SimplifiesToAssignment()
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
            """,
            """
            public class C
            {
                public void M(bool cond)
                {
                    bool x;
                    x = cond;
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
            """,
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    bool x;
                    x = a && b;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
