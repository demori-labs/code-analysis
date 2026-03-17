using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes;
using DemoriLabs.CodeAnalysis.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class RecordPrimaryConstructorTooManyParametersCodeFixTests
{
    private static CSharpCodeFixTest<
        RecordPrimaryConstructorTooManyParametersAnalyzer,
        RecordPrimaryConstructorTooManyParametersCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource, int? threshold = null)
    {
        var test = new CSharpCodeFixTest<
            RecordPrimaryConstructorTooManyParametersAnalyzer,
            RecordPrimaryConstructorTooManyParametersCodeFix,
            DefaultVerifier
        >
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        if (threshold.HasValue)
        {
            test.TestState.AnalyzerConfigFiles.Add(
                (
                    "/.editorconfig",
                    $"""
                    root = true

                    [*]
                    dotnet_diagnostic.DL1003.positional_parameters_threshold = {threshold.Value}
                    """
                )
            );

            test.FixedState.AnalyzerConfigFiles.Add(
                (
                    "/.editorconfig",
                    $"""
                    root = true

                    [*]
                    dotnet_diagnostic.DL1003.positional_parameters_threshold = {threshold.Value}
                    """
                )
            );
        }

        return test;
    }

    [Test]
    public async Task ConvertsParametersToExplicitRequiredProperties()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string FirstName, string LastName, int Age, string Email, string Phone, string Address);
            """,
            """
            public record Person
            {
                public required string FirstName { get; init; }
                public required string LastName { get; init; }
                public required int Age { get; init; }
                public required string Email { get; init; }
                public required string Phone { get; init; }
                public required string Address { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CustomThreshold_ConvertsParameters()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string FirstName, string LastName, int Age);
            """,
            """
            public record Person
            {
                public required string FirstName { get; init; }
                public required string LastName { get; init; }
                public required int Age { get; init; }
            }
            """,
            threshold: 2
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OptionalParameters_NotRequired_WithDefaultValue()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age = 0, string? Country = "UK");
            """,
            """
            public record Person
            {
                public required string Name { get; init; }
                public int Age { get; init; } = 0;
                public string? Country { get; init; } = "UK";
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AllOptionalParameters()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Config|}(int Retries = 3, bool Verbose = false, string Env = "prod");
            """,
            """
            public record Config
            {
                public int Retries { get; init; } = 3;
                public bool Verbose { get; init; } = false;
                public string Env { get; init; } = "prod";
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesExistingComputedProperty()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string FirstName, string LastName, int Age, string Email, string Phone, string Address)
            {
                public string FullName => FirstName + " " + LastName;
            }
            """,
            """
            public record Person
            {
                public required string FirstName { get; init; }
                public required string LastName { get; init; }
                public required int Age { get; init; }
                public required string Email { get; init; }
                public required string Phone { get; init; }
                public required string Address { get; init; }
                public string FullName => FirstName + " " + LastName;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordStruct_ConvertsParameters()
    {
        var test = CreateTest(
            """
            public record struct {|DL1003:Point|}(int X, int Y, int Z, int W, int U, int V);
            """,
            """
            public record struct Point
            {
                public required int X { get; set; }
                public required int Y { get; set; }
                public required int Z { get; set; }
                public required int W { get; set; }
                public required int U { get; set; }
                public required int V { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadOnlyRecordStruct_ConvertsParameters()
    {
        var test = CreateTest(
            """
            public readonly record struct {|DL1003:Point|}(int X, int Y, int Z, int W, int U, int V);
            """,
            """
            public readonly record struct Point
            {
                public required int X { get; init; }
                public required int Y { get; init; }
                public required int Z { get; init; }
                public required int W { get; init; }
                public required int U { get; init; }
                public required int V { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordStruct_WithExistingBody()
    {
        var test = CreateTest(
            """
            public record struct {|DL1003:Point|}(int X, int Y, int Z, int W, int U, int V)
            {
                public double Length => System.Math.Sqrt(X * X + Y * Y + Z * Z);
            }
            """,
            """
            public record struct Point
            {
                public required int X { get; set; }
                public required int Y { get; set; }
                public required int Z { get; set; }
                public required int W { get; set; }
                public required int U { get; set; }
                public required int V { get; set; }
                public double Length => System.Math.Sqrt(X * X + Y * Y + Z * Z);
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleConstructor_RewritesThisInitializer()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age)
            {
                public Person(string name) : this(name, 0) { }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; }

                public Person(string name)
                {
                    Name = name;
                    Age = 0;
                }
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleConstructor_WithExpressionArguments()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age)
            {
                public Person(string name) : this(name.Trim(), name.Length) { }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; }

                public Person(string name)
                {
                    Name = name.Trim();
                    Age = name.Length;
                }
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleConstructor_WithExistingBodyStatements()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age)
            {
                public Person(string name) : this(name, 0)
                {
                    System.Console.WriteLine("Created");
                }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; }

                public Person(string name)
                {
                    Name = name;
                    Age = 0;
                    System.Console.WriteLine("Created");
                }
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleConstructors_AllChainingToThis()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age, string Email)
            {
                public Person(string x) : this(x, 0, "") { }
                public Person(string x, int y) : this(x, y, "") { }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; }
                public string Email { get; init; }

                public Person(string x)
                {
                    Name = x;
                    Age = 0;
                    Email = "";
                }

                public Person(string x, int y)
                {
                    Name = x;
                    Age = y;
                    Email = "";
                }
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Constructor_ChainingToOtherConstructor_NotToPrimary()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age)
            {
                public Person(string name) : this(name, 0) { }
                public Person() : this("Unknown") { }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; }

                public Person(string name)
                {
                    Name = name;
                    Age = 0;
                }

                public Person() : this("Unknown") {}
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Constructor_MixOfOptionalAndRequired_WithThis()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age = 0)
            {
                public Person() : this("Anonymous") { }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; } = 0;

                public Person()
                {
                    Name = "Anonymous";
                }
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }
}
