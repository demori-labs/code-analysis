using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class LogicalPatternCodeFixTests
{
    private static CSharpCodeFixTest<LogicalPatternAnalyzer, LogicalPatternCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<LogicalPatternAnalyzer, LogicalPatternCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task OrChain_FixesToOrPattern()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x == 1 || x == 2 || x == 3|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is 1 or 2 or 3) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RangeCheck_FixesToAndPattern()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x >= 0 && x < 100|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is >= 0 and < 100) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsChain_FixesToNotAndPattern()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x != 1 && x != 2|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is not 1 and not 2) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
