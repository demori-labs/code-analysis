using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.NamedArgument;
using DemoriLabs.CodeAnalysis.NamedArgument;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NamedArgument;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NamedArgumentCodeFixTests
{
    private static CSharpCodeFixTest<NamedArgumentAnalyzer, NamedArgumentCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource,
        int? namedArgumentsThreshold = null
    )
    {
        var test = new CSharpCodeFixTest<NamedArgumentAnalyzer, NamedArgumentCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipFixAllCheck,
        };

        if (namedArgumentsThreshold.HasValue)
        {
            var config = $"""
                root = true

                [*]
                dotnet_diagnostic.DL3001.named_arguments_threshold = {namedArgumentsThreshold.Value}
                """;

            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
            test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        }

        return test;
    }

    [Test]
    public async Task Threshold_MismatchedName_AddsParameterName()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var n = "Alice";
                    var a = 30;
                    var e = "a@b.com";
                    Foo({|DL3001:n|}, {|DL3001:a|}, {|DL3001:e|});
                }
                private static void Foo(string name, int age, string email) { }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                    var n = "Alice";
                    var a = 30;
                    var e = "a@b.com";
                    Foo(name: n, age: a, email: e);
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_Literal_AddsParameterName()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    Foo({|DL3001:"Alice"|}, {|DL3001:30|}, {|DL3001:"a@b.com"|});
                }
                private static void Foo(string name, int age, string email) { }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                    Foo(name: "Alice", age: 30, email: "a@b.com");
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_FixesOnlyMismatched()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var years = 30;
                    var email = "a@b.com";
                    Foo(name, {|DL3001:years|}, email);
                }
                private static void Foo(string name, int age, string email) { }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var years = 30;
                    var email = "a@b.com";
                    Foo(name, age: years, email);
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NamedArgumentAttribute_AddsParameterName()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M() => Foo({|DL3001:true|});
                private static void Foo([NamedArgument] bool enabled) { }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M() => Foo(enabled: true);
                private static void Foo([NamedArgument] bool enabled) { }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(NamedArgumentAttribute).Assembly);
        test.FixedState.AdditionalReferences.Add(typeof(NamedArgumentAttribute).Assembly);
        await test.RunAsync();
    }
}
