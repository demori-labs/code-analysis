using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.Attributes;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.ReadOnlyParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.ReadOnlyParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class SuggestReadOnlyPrimaryConstructorParameterCodeFixTests
{
    private static CSharpCodeFixTest<
        SuggestReadOnlyPrimaryConstructorParameterAnalyzer,
        SuggestReadOnlyPrimaryConstructorParameterCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        var test = new CSharpCodeFixTest<
            SuggestReadOnlyPrimaryConstructorParameterAnalyzer,
            SuggestReadOnlyPrimaryConstructorParameterCodeFix,
            DefaultVerifier
        >
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task AddsReadOnlyAttribute()
    {
        var test = CreateTest(
            """
            public class Widget(int {|DL2003:count|});
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class Widget([ReadOnly] int count);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AddsReadOnlyAttribute_MultipleParameters()
    {
        var test = CreateTest(
            """
            public class Widget(string {|DL2003:name|}, int {|DL2003:count|});
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class Widget([ReadOnly] string name, [ReadOnly] int count);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DoesNotDuplicateUsing_WhenAlreadyPresent()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class Widget(int {|DL2003:count|});
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class Widget([ReadOnly] int count);
            """
        );

        await test.RunAsync();
    }
}
