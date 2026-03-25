using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class AsWithNullCheckCodeFixTests
{
    private static CSharpCodeFixTest<AsWithNullCheckAnalyzer, AsWithNullCheckCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<AsWithNullCheckAnalyzer, AsWithNullCheckCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task AsWithIsNotNull_FixesToDeclarationPattern()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o {|DL3007:as Animal|};
                    if (a is not null)
                    {
                        System.Console.WriteLine(a);
                    }
                }
            }
            """,
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    if (o is Animal a)
                    {
                        System.Console.WriteLine(a);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsWithNotEqualsNull_FixesToDeclarationPattern()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o {|DL3007:as Animal|};
                    if (a != null)
                    {
                        System.Console.WriteLine(a);
                    }
                }
            }
            """,
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    if (o is Animal a)
                    {
                        System.Console.WriteLine(a);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GuardClauseWithThrow_FixesToDeclarationPattern()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o {|DL3007:as Animal|};
                    if (a is null)
                    {
                        throw new System.Exception("not an animal");
                    }
                    System.Console.WriteLine(a);
                }
            }
            """,
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    if (o is not Animal a)
                    {
                        throw new System.Exception("not an animal");
                    }
                    System.Console.WriteLine(a);
                }
            }
            """
        );

        await test.RunAsync();
    }
}
