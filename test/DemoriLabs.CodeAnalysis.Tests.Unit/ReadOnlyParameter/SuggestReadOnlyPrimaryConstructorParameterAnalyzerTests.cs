using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.ReadOnlyParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.ReadOnlyParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class SuggestReadOnlyPrimaryConstructorParameterAnalyzerTests
{
    private static CSharpAnalyzerTest<SuggestReadOnlyPrimaryConstructorParameterAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        var test = new CSharpAnalyzerTest<SuggestReadOnlyPrimaryConstructorParameterAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task ClassPrimaryConstructor_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Widget(int {|DL2003:count|});
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassPrimaryConstructor_MultipleParameters_ReportsAll()
    {
        var test = CreateTest(
            """
            public class Widget(string {|DL2003:name|}, int {|DL2003:count|});
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassPrimaryConstructor_AlreadyReadOnly_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Widget([ReadOnly] int count);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordPrimaryConstructor_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Widget(int Count);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordStructPrimaryConstructor_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record struct Point(int X, int Y);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StructPrimaryConstructor_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public struct Point(int x, int y)
            {
                public int X { get; } = x;
                public int Y { get; } = y;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RegularMethodParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassPrimaryConstructor_RefParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Widget(ref int count)
            {
                public int Count { get; } = count;
            }
            """
        );

        await test.RunAsync();
    }
}
