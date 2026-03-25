using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.NullSimplification;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NullSimplification;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NullCoalescingCodeFixTests
{
    private static CSharpCodeFixTest<NullCoalescingAnalyzer, NullCoalescingCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<NullCoalescingAnalyzer, NullCoalescingCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task Ternary_NotEqualsNull_ReplacesWithCoalescing()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x != null {|DL3013:?|} x : y;
                }
            }
            """,
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x ?? y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Ternary_EqualsNull_Reversed_ReplacesWithCoalescing()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x == null {|DL3013:?|} y : x;
                }
            }
            """,
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x ?? y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStatement_FallThrough_ReplacesWithCoalescing()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    {|DL3013:if|} (x != null)
                    {
                        return x;
                    }
                    return y;
                }
            }
            """,
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x ?? y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStatement_WithElse_ReplacesWithCoalescing()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    {|DL3013:if|} (x != null)
                    {
                        return x;
                    }
                    else
                    {
                        return y;
                    }
                }
            }
            """,
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x ?? y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Ternary_IsNotNull_Pattern_ReplacesWithCoalescing()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x is not null {|DL3013:?|} x : y;
                }
            }
            """,
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x ?? y;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Ternary_IsNull_Reversed_ReplacesWithCoalescing()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x is null {|DL3013:?|} y : x;
                }
            }
            """,
            """
            public class C
            {
                public string M(string? x, string y)
                {
                    return x ?? y;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
