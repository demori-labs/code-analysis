using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class RecordPrimaryConstructorTooManyParametersAnalyzerTests
{
    private static CSharpAnalyzerTest<RecordPrimaryConstructorTooManyParametersAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        int? threshold = null
    )
    {
        var test = new CSharpAnalyzerTest<RecordPrimaryConstructorTooManyParametersAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        if (threshold.HasValue)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig",
                $"""
                root = true

                [*]
                dotnet_diagnostic.DL1003.positional_parameters_threshold = {threshold.Value}
                """));
        }

        return test;
    }

    [Test]
    public async Task RecordWithSixParameters_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string FirstName, string LastName, int Age, string Email, string Phone, string Address);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithFiveParameters_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string FirstName, string LastName, int Age, string Email, string Phone);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithFourParameters_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person(string FirstName, string LastName, int Age, string Email);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithNoParameters_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person;
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithEmptyParameterList_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person();
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordStructWithSixParameters_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record struct {|DL1003:Point|}(int X, int Y, int Z, int W, int U, int V);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadOnlyRecordStructWithSixParameters_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public readonly record struct {|DL1003:Point|}(int X, int Y, int Z, int W, int U, int V);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassPrimaryConstructor_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Person(string firstName, string lastName, int age, string email, string phone, string address);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StructPrimaryConstructor_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public struct Point(int x, int y, int z, int w, int u, int v);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CustomThreshold_ThreeParameters_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string FirstName, string LastName, int Age);
            """,
            threshold: 2
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CustomThreshold_TwoParameters_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person(string FirstName, string LastName);
            """,
            threshold: 2
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThresholdZero_SingleParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name);
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithBodyAndTooManyParameters_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string FirstName, string LastName, int Age, string Email, string Phone, string Address)
            {
                public string FullName => FirstName + " " + LastName;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThresholdZero_RecordWithExplicitConstructor_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record {|DL1003:Person|}(string Name, int Age)
            {
                public Person(string name) : this(name, 0) { }
            }
            """,
            threshold: 0
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithExplicitPropertiesOnly_NoDiagnostic()
    {
        var test = CreateTest(
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
}
