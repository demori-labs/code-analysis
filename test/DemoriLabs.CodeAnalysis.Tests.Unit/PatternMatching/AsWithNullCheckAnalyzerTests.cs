using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class AsWithNullCheckAnalyzerTests
{
    private static CSharpAnalyzerTest<AsWithNullCheckAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<AsWithNullCheckAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task AsFollowedByNullCheck_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsFollowedByEqualsNullCheck_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GuardClauseWithThrow_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GuardClauseWithReturn_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o {|DL3007:as Animal|};
                    if (a == null)
                    {
                        return;
                    }
                    System.Console.WriteLine(a);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GuardClauseWithElse_ReportsDiagnostic()
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
                    else
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
    public async Task GuardClauseWithoutExit_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o as Animal;
                    if (a is null)
                    {
                        System.Console.WriteLine("null");
                    }
                    System.Console.WriteLine(a);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsWithoutNullCheck_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o as Animal;
                    System.Console.WriteLine(a);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsUsedBeforeNullCheck_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o as Animal;
                    System.Console.WriteLine(a);
                    if (a is not null)
                    {
                        System.Console.WriteLine("not null");
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsUsedAfterIfStatement_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o as Animal;
                    if (a is not null)
                    {
                        System.Console.WriteLine("not null");
                    }
                    System.Console.WriteLine(a);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AsUsedInElseBranch_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o as Animal;
                    if (a is not null)
                    {
                        System.Console.WriteLine("not null");
                    }
                    else
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
    public async Task AlreadyDeclarationPattern_NoDiagnostic()
    {
        var test = CreateTest(
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
    public async Task InsideExpressionTree_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq;
            using System.Linq.Expressions;

            public class Animal { }

            public class C
            {
                public void M(IQueryable<object> q)
                {
                    q.Select(o => o as Animal);
                }

                public void N()
                {
                    Expression<Func<object, Animal?>> expr = x => x as Animal;
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
            public class Animal { }

            public class C
            {
                public C(object o)
                {
                    var a = o {|DL3007:as Animal|};
                    if (a is not null)
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
    public async Task MultipleStatementsBeforeIf_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Animal { }

            public class C
            {
                public void M(object o)
                {
                    var a = o as Animal;
                    var x = 42;
                    if (a is not null)
                    {
                        System.Console.WriteLine(a);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
