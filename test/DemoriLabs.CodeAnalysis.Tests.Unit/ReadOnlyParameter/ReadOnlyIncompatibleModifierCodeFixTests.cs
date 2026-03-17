using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes;
using DemoriLabs.CodeAnalysis.ReadOnlyParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.ReadOnlyParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ReadOnlyIncompatibleModifierCodeFixTests
{
    private static CSharpCodeFixTest<
        ReadOnlyParameterAnalyzer,
        ReadOnlyIncompatibleModifierCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        var test = new CSharpCodeFixTest<
            ReadOnlyParameterAnalyzer,
            ReadOnlyIncompatibleModifierCodeFix,
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
    public async Task RefParameter_RemovesReadOnlyAttribute()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([{|DL2002:ReadOnly|}] ref int x) { }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M(ref int x) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OutParameter_RemovesReadOnlyAttribute()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([{|DL2002:ReadOnly|}] out int x) { x = 0; }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M(out int x) { x = 0; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InParameter_RemovesReadOnlyAttribute()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([{|DL2002:ReadOnly|}] in int x) { }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M(in int x) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordClassPrimaryConstructor_RemovesReadOnlyAttribute()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public record Widget([{|DL2002:ReadOnly|}] int Count);
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public record Widget(int Count);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadonlyRecordStruct_RemovesReadOnlyAttribute()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public readonly record struct Point([{|DL2002:ReadOnly|}] int X);
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public readonly record struct Point(int X);
            """
        );

        await test.RunAsync();
    }
}
