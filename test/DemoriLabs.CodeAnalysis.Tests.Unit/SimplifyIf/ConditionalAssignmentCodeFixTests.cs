using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.SimplifyIf;
using DemoriLabs.CodeAnalysis.SimplifyIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.SimplifyIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ConditionalAssignmentCodeFixTests
{
    private static CSharpCodeFixTest<
        ConditionalAssignmentAnalyzer,
        ConditionalAssignmentCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<ConditionalAssignmentAnalyzer, ConditionalAssignmentCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task Standard_SimplifiesToTernary()
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
            """,
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    x = cond ? 1 : 2;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockBodies_SimplifiesToTernary()
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
            """,
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    x = cond ? 1 : 2;
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
            """,
            """
            public class C
            {
                public void M(bool cond)
                {
                    int x;
                    x = cond ? Foo() : Bar();
                }

                private int Foo() => 1;
                private int Bar() => 2;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PropertyTarget_PreservedInFix()
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
            """,
            """
            public class C
            {
                public int Value { get; set; }

                public void M(bool cond, int a, int b)
                {
                    var obj = new C();
                    obj.Value = cond ? a : b;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
