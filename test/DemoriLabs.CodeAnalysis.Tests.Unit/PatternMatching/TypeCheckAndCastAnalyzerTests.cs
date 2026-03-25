using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class TypeCheckAndCastAnalyzerTests
{
    private static CSharpAnalyzerTest<TypeCheckAndCastAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<TypeCheckAndCastAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task IsTypeFollowedByCast_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if ({|DL3006:o is string|})
                    {
                        var s = (string)o;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsTypeWithCastInReturn_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string? M(object o)
                {
                    if ({|DL3006:o is string|})
                    {
                        return (string)o;
                    }
                    return null;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsTypeWithMultipleCasts_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if ({|DL3006:o is string|})
                    {
                        var len = ((string)o).Length;
                        System.Console.WriteLine((string)o);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsTypeNoCast_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if (o is string)
                    {
                        System.Console.WriteLine("it's a string");
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AlreadyDeclarationPattern_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if (o is string s)
                    {
                        System.Console.WriteLine(s);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsTypeNotInIfCondition_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(object o) => o is string;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideExpressionTree_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq;
            using System.Linq.Expressions;

            public class C
            {
                public void M(IQueryable<object> q)
                {
                    q.Where(o => o is string);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CastToDifferentType_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if (o is string)
                    {
                        var i = (int)o;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InConstructor_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public C(object o)
                {
                    if ({|DL3006:o is string|})
                    {
                        var s = (string)o;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
