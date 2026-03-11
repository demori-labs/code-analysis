using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class RecordsShouldNotHaveMutablePropertiesAnalyzerTests
{
    private static CSharpAnalyzerTest<RecordsShouldNotHaveMutablePropertiesAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<RecordsShouldNotHaveMutablePropertiesAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task RecordWithSetProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string {|DL1001:Name|} { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithMultipleSetProperties_ReportsMultipleDiagnostics()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string {|DL1001:Name|} { get; set; }
                public int {|DL1001:Age|} { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithInitProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string Name { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithGetOnlyProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string Name { get; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithPositionalParameters_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person(string Name, int Age);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithMixedProperties_ReportsDiagnosticOnlyForSet()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string {|DL1001:Name|} { get; set; }
                public int Age { get; init; }
                public string Id { get; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RegularClass_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Person
            {
                public string Name { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Struct_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public struct Point
            {
                public int X { get; set; }
                public int Y { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordStruct_WithSetProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record struct Point
            {
                public int X { get; set; }
                public int Y { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadOnlyRecordStruct_WithInitProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public readonly record struct Point
            {
                public int X { get; init; }
                public int Y { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithPositionalAndBodySetProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person(string Name)
            {
                public int {|DL1001:Age|} { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EmptyRecord_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Empty;
            """
        );

        await test.RunAsync();
    }
}
