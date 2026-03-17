using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.Attributes;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class DataClassCouldBeRecordCodeFixTests
{
    private static CSharpCodeFixTest<
        DataClassCouldBeRecordAnalyzer,
        DataClassCouldBeRecordCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        var test = new CSharpCodeFixTest<DataClassCouldBeRecordAnalyzer, DataClassCouldBeRecordCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(MutableAttribute).Assembly);
        test.FixedState.AdditionalReferences.Add(typeof(MutableAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task ConvertsSetToInitAndAddsRequired()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
            """,
            """
            public record Person
            {
                public required string Name { get; init; }
                public required int Age { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesInitProperties_AddsRequired()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; init; }
                public int Age { get; init; }
            }
            """,
            """
            public record Person
            {
                public required string Name { get; init; }
                public required int Age { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PropertyWithDefault_NoRequired()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Config|}
            {
                public string Name { get; set; }
                public int Retries { get; set; } = 3;
            }
            """,
            """
            public record Config
            {
                public required string Name { get; init; }
                public int Retries { get; init; } = 3;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesGetOnlyProperties()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; set; }
                public string Upper => Name.ToUpper();
            }
            """,
            """
            public record Person
            {
                public required string Name { get; init; }
                public string Upper => Name.ToUpper();
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesConstructor()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public Person(string name)
                {
                    Name = name;
                }

                public string Name { get; set; }
            }
            """,
            """
            public record Person
            {
                public Person(string name)
                {
                    Name = name;
                }

                public required string Name { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GenericClass_ConvertsToRecord()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Wrapper|}<T>
            {
                public T Value { get; set; }
            }
            """,
            """
            public record Wrapper<T>
            {
                public required T Value { get; init; }
            }
            """
        );

        await test.RunAsync();
    }
}
