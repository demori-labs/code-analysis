using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.StringEquals;
using DemoriLabs.CodeAnalysis.StringEquals;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.StringEquals;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UseStringEqualsCodeFixTests
{
    private static CSharpCodeFixTest<UseStringEqualsAnalyzer, UseStringEqualsCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<UseStringEqualsAnalyzer, UseStringEqualsCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsLiteral_FixesToStringEquals()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s == "hello"|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if (string.Equals(s, "hello", StringComparison.Ordinal)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsLiteral_FixesToStringEqualsIsFalse()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s != "hello"|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if (string.Equals(s, "hello", StringComparison.Ordinal) is false) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReversedOperandOrder_PreservesOrder()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:"hello" == s|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if (string.Equals("hello", s, StringComparison.Ordinal)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoStringVariables_FixesToStringEquals()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string a, string b)
                {
                    if ({|DL3017:a == b|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string a, string b)
                {
                    if (string.Equals(a, b, StringComparison.Ordinal)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsStringLiteral_FixesToStringEquals()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s is "hello"|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if (string.Equals(s, "hello", StringComparison.Ordinal)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotStringLiteral_FixesToStringEqualsIsFalse()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s is not "hello"|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if (string.Equals(s, "hello", StringComparison.Ordinal) is false) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AddsUsingSystemWhenMissing()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s == "hello"|}) { }
                }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if (string.Equals(s, "hello", StringComparison.Ordinal)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
