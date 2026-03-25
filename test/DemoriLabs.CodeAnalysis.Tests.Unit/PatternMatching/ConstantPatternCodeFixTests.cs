using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ConstantPatternCodeFixTests
{
    private static CSharpCodeFixTest<ConstantPatternAnalyzer, ConstantPatternCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<ConstantPatternAnalyzer, ConstantPatternCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsNull_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o {|DL3003:== null|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsNull_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o {|DL3003:!= null|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReversedNull_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:null ==|} o) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsBoolTrue_FixesToIsTrue()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b {|DL3003:== true|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b is true) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsTrue_FixesToIsNotTrue()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b {|DL3003:!= true|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b is not true) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsIntLiteral_FixesToIsLiteral()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x {|DL3003:== 42|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is 42) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsIntLiteral_FixesToIsNotLiteral()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x {|DL3003:!= 0|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is not 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegativeIntLiteral_FixesToIsWithParentheses()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x {|DL3003:== -1|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is -1) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsEnumMember_FixesToIsEnumMember()
    {
        var test = CreateTest(
            """
            public enum Color { Red, Green, Blue }

            public class C
            {
                public void M(Color c)
                {
                    if (c {|DL3003:== Color.Red|}) { }
                }
            }
            """,
            """
            public enum Color { Red, Green, Blue }

            public class C
            {
                public void M(Color c)
                {
                    if (c is Color.Red) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
