using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.UnusedParameter;
using DemoriLabs.CodeAnalysis.UnusedParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.UnusedParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UnusedParameterCodeFixTests
{
    private static CSharpCodeFixTest<UnusedParameterAnalyzer, UnusedParameterCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<UnusedParameterAnalyzer, UnusedParameterCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task RemovesUnusedParameter()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int {|DL2005:unused|})
                {
                }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RemovesUnusedParameter_KeepsOtherParams()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x, int {|DL2005:unused|}, int z)
                {
                    System.Console.WriteLine(x + z);
                }
            }
            """,
            """
            public class C
            {
                public void M(int x, int z)
                {
                    System.Console.WriteLine(x + z);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RemovesUnusedParameter_UpdatesCallSite()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x, int {|DL2005:unused|})
                {
                    System.Console.WriteLine(x);
                }

                public void Caller()
                {
                    M(1, 2);
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    System.Console.WriteLine(x);
                }

                public void Caller()
                {
                    M(1);
                }
            }
            """
        );

        await test.RunAsync();
    }
}
