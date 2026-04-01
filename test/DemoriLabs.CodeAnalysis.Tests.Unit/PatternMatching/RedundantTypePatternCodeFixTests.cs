using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class RedundantTypePatternCodeFixTests
{
    private static CSharpCodeFixTest<
        RedundantTypePatternAnalyzer,
        RedundantTypePatternCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<RedundantTypePatternAnalyzer, RedundantTypePatternCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task NullableReferenceType_IsType_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public void M(string? str)
                {
                    if ({|DL3016:str is string|})
                    {
                        System.Console.WriteLine(str);
                    }
                }
            }
            """,
            """
            #nullable enable
            public class C
            {
                public void M(string? str)
                {
                    if (str is not null)
                    {
                        System.Console.WriteLine(str);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullableSubtypeToBase_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class Animal { }
            public class Dog : Animal { }

            public class C
            {
                public void M(Dog? dog)
                {
                    if ({|DL3016:dog is Animal|})
                    {
                        System.Console.WriteLine(dog);
                    }
                }
            }
            """,
            """
            #nullable enable
            public class Animal { }
            public class Dog : Animal { }

            public class C
            {
                public void M(Dog? dog)
                {
                    if (dog is not null)
                    {
                        System.Console.WriteLine(dog);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_NoFixOffered()
    {
        var test = new CSharpCodeFixTest<RedundantTypePatternAnalyzer, RedundantTypePatternCodeFix, DefaultVerifier>
        {
            TestCode = """
                public class C
                {
                    public void M(int c)
                    {
                        if ({|DL3016:c is int|})
                        {
                            System.Console.WriteLine(c);
                        }
                    }
                }
                """,
            FixedCode = """
                public class C
                {
                    public void M(int c)
                    {
                        if ({|DL3016:c is int|})
                        {
                            System.Console.WriteLine(c);
                        }
                    }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        await test.RunAsync();
    }

    [Test]
    public async Task NonNullableReferenceType_NoFixOffered()
    {
        var test = new CSharpCodeFixTest<RedundantTypePatternAnalyzer, RedundantTypePatternCodeFix, DefaultVerifier>
        {
            TestCode = """
                #nullable enable
                public class C
                {
                    public void M(string str)
                    {
                        if ({|DL3016:str is string|})
                        {
                            System.Console.WriteLine(str);
                        }
                    }
                }
                """,
            FixedCode = """
                #nullable enable
                public class C
                {
                    public void M(string str)
                    {
                        if ({|DL3016:str is string|})
                        {
                            System.Console.WriteLine(str);
                        }
                    }
                }
                """,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        await test.RunAsync();
    }
}
