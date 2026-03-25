using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.NullSimplification;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NullSimplification;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NullConditionalAssignmentCodeFixTests
{
    private static CSharpCodeFixTest<
        NullConditionalAssignmentAnalyzer,
        NullConditionalAssignmentCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<
            NullConditionalAssignmentAnalyzer,
            NullConditionalAssignmentCodeFix,
            DefaultVerifier
        >
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task PropertyAssignment_ReplacesWithNullConditional()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x is not null)
                    {
                        x.Prop = 42;
                    }
                }
            }
            """,
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    x?.Prop = 42;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodCall_ReplacesWithNullConditional()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void DoWork(int value) { }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x is not null)
                    {
                        x.DoWork(5);
                    }
                }
            }
            """,
            """
            public class C
            {
                public void DoWork(int value) { }
            }

            public class Test
            {
                public void M(C? x)
                {
                    x?.DoWork(5);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CompoundAssignment_ReplacesWithNullConditional()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Count { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x is not null)
                    {
                        x.Count += 1;
                    }
                }
            }
            """,
            """
            public class C
            {
                public int Count { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    x?.Count += 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockBody_ReplacesWithNullConditional()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x != null)
                    {
                        x.Prop = 42;
                    }
                }
            }
            """,
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    x?.Prop = 42;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
