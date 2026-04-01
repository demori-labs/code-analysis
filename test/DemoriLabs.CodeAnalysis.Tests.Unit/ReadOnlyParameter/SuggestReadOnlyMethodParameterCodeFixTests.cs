using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.ReadOnlyParameter;
using DemoriLabs.CodeAnalysis.ReadOnlyParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.ReadOnlyParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class SuggestReadOnlyMethodParameterCodeFixTests
{
    private static CSharpCodeFixTest<
        SuggestReadOnlyMethodParameterAnalyzer,
        SuggestReadOnlyMethodParameterCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        var test = new CSharpCodeFixTest<
            SuggestReadOnlyMethodParameterAnalyzer,
            SuggestReadOnlyMethodParameterCodeFix,
            DefaultVerifier
        >
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        test.FixedState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task AddsReadOnlyAttribute()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2004:x|})
                {
                    System.Console.WriteLine(x);
                }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AddsReadOnlyAttribute_ExistingUsing()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M(int {|DL2004:x|})
                {
                    System.Console.WriteLine(x);
                }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    System.Console.WriteLine(x);
                }
            }
            """
        );

        await test.RunAsync();
    }
}
