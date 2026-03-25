using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class TypeCheckAndCastCodeFixTests
{
    private static CSharpCodeFixTest<TypeCheckAndCastAnalyzer, TypeCheckAndCastCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<TypeCheckAndCastAnalyzer, TypeCheckAndCastCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task IsTypeWithCast_FixesToDeclarationPattern()
    {
        var test = CreateTest(
            """
            public class Animal { public string Name => ""; }

            public class C
            {
                public void M(object o)
                {
                    if ({|DL3006:o is Animal|})
                    {
                        var a = (Animal)o;
                    }
                }
            }
            """,
            """
            public class Animal { public string Name => ""; }

            public class C
            {
                public void M(object o)
                {
                    if (o is Animal animal)
                    {
                        var a = animal;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsTypeWithCastInReturn_FixesToDeclarationPattern()
    {
        var test = CreateTest(
            """
            public class Animal { public string Name => ""; }

            public class C
            {
                public Animal? M(object o)
                {
                    if ({|DL3006:o is Animal|})
                    {
                        return (Animal)o;
                    }
                    return null;
                }
            }
            """,
            """
            public class Animal { public string Name => ""; }

            public class C
            {
                public Animal? M(object o)
                {
                    if (o is Animal animal)
                    {
                        return animal;
                    }
                    return null;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsTypeWithMultipleCasts_FixesAll()
    {
        var test = CreateTest(
            """
            public class Animal { public string Name => ""; }

            public class C
            {
                public void M(object o)
                {
                    if ({|DL3006:o is Animal|})
                    {
                        var name = ((Animal)o).Name;
                        System.Console.WriteLine((Animal)o);
                    }
                }
            }
            """,
            """
            public class Animal { public string Name => ""; }

            public class C
            {
                public void M(object o)
                {
                    if (o is Animal animal)
                    {
                        var name = (animal).Name;
                        System.Console.WriteLine(animal);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
