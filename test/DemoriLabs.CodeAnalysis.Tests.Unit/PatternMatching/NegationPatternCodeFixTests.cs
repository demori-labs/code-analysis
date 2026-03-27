using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NegationPatternCodeFixTests
{
    private static CSharpCodeFixTest<NegationPatternAnalyzer, NegationPatternCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<NegationPatternAnalyzer, NegationPatternCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task NegatedBool_FixesToIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool flag)
                {
                    if ({|DL3004:!flag|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(bool flag)
                {
                    if (flag is false) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedMethodCall_FixesToIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3004:!string.IsNullOrEmpty(s)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string s)
                {
                    if (string.IsNullOrEmpty(s) is false) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedIsType_FixesToIsNot()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if ({|DL3004:!(o is string)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(object o)
                {
                    if (o is not string) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedIsNull_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3004:!(o is null)|}) { }
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
    public async Task NegatedIsPattern_FixesToIsNotPattern()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3004:!(x is > 5)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is not > 5) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedEqualsNull_FixesToIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3004:!(o == null)|}) { }
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
    public async Task NegatedNotEqualsNull_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3004:!(o != null)|}) { }
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
    public async Task NegatedHasValue_FixesToIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3004:!id.HasValue|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedIsNotNull_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3004:!(o is not null)|}) { }
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
    public async Task NegatedIsNotString_FixesToIsString()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if ({|DL3004:!(o is not string)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(object o)
                {
                    if (o is string) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_NegatedEqualsNull_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3004:!(id == null)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
