using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.NamedArgument;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.NamedArgument;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NamedArgumentCodeFixTests
{
    private static CSharpCodeFixTest<NamedArgumentAnalyzer, NamedArgumentCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<NamedArgumentAnalyzer, NamedArgumentCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task BoolLiteral_AddsParameterName()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:true|});
                private static void Foo(bool enabled) { }
            }
            """,
            """
            public class C
            {
                public void M() => Foo(enabled: true);
                private static void Foo(bool enabled) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NumericLiteral_AddsParameterName()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:42|});
                private static void Foo(int count) { }
            }
            """,
            """
            public class C
            {
                public void M() => Foo(count: 42);
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MismatchedVariableName_AddsParameterName()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var total = 5;
                    Foo({|DL3001:total|});
                }
                private static void Foo(int count) { }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                    var total = 5;
                    Foo(count: total);
                }
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleArguments_FixesOnlyFlagged()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var years = 30;
                    Foo(name, {|DL3001:years|});
                }
                private static void Foo(string name, int age) { }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var years = 30;
                    Foo(name, age: years);
                }
                private static void Foo(string name, int age) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullLiteral_AddsParameterName()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:null|});
                private static void Foo(string? value) { }
            }
            """,
            """
            public class C
            {
                public void M() => Foo(value: null);
                private static void Foo(string? value) { }
            }
            """
        );

        await test.RunAsync();
    }
}
