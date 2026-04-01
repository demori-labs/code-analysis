using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.EmptyString;
using DemoriLabs.CodeAnalysis.EmptyString;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.EmptyString;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UseIsNullOrEmptyCodeFixTests
{
    private static CSharpCodeFixTest<UseIsNullOrEmptyAnalyzer, UseIsNullOrEmptyCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<UseIsNullOrEmptyAnalyzer, UseIsNullOrEmptyCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsEmpty_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s == ""|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsEmpty_FixesToIsNullOrEmptyIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s != ""|}) { }
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
    public async Task LengthEqualsZero_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Length == 0|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LengthIsZero_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Length is 0|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullConditionalLength_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if ({|DL5002:s?.Length == 0|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string? s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsEmptyString_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s is ""|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotEmptyString_FixesToIsNullOrEmptyIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s is not ""|}) { }
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
    public async Task IsNullOrEmpty_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if ({|DL5002:s is null or ""|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string? s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNullAndNotEmpty_FixesToIsNullOrEmptyIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if ({|DL5002:s is not null and not ""|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string? s)
                {
                    if (string.IsNullOrEmpty(s) is false) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StaticStringEqualsWithComparison_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:string.Equals(s, "", StringComparison.Ordinal)|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsStringEmpty_FixesToIsNullOrEmpty()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s == string.Empty|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(string s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
