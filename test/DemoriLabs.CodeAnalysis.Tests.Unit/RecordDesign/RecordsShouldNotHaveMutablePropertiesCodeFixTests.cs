using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.RecordDesign;
using DemoriLabs.CodeAnalysis.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class RecordsShouldNotHaveMutablePropertiesCodeFixTests
{
    private static CSharpCodeFixTest<
        RecordsShouldNotHaveMutablePropertiesAnalyzer,
        RecordsShouldNotHaveMutablePropertiesCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<
            RecordsShouldNotHaveMutablePropertiesAnalyzer,
            RecordsShouldNotHaveMutablePropertiesCodeFix,
            DefaultVerifier
        >
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task SetAccessor_ReplacedWithInit()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string {|DL1001:Name|} { get; set; }
            }
            """,
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
    public async Task MultipleProperties_FixesAll()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string {|DL1001:Name|} { get; set; }
                public int {|DL1001:Age|} { get; set; }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InitAccessor_NotChanged()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string {|DL1001:Name|} { get; set; }
                public int Age { get; init; }
            }
            """,
            """
            public record Person
            {
                public string Name { get; init; }
                public int Age { get; init; }
            }
            """
        );

        await test.RunAsync();
    }
}
