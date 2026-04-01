using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class RedundantTypePatternAnalyzerTests
{
    private static CSharpAnalyzerTest<RedundantTypePatternAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<RedundantTypePatternAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task ValueType_IsType_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_IsTypeWithDeclaration_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int c)
                {
                    if ({|DL3016:c is int x|})
                    {
                        System.Console.WriteLine(x);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonNullableReferenceType_IsType_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonNullableReferenceType_IsTypeWithDeclaration_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public void M(string str)
                {
                    if ({|DL3016:str is string s|})
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
    public async Task NullableReferenceType_IsTypeWithoutVariable_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SubtypeToBase_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class Animal { }
            public class Dog : Animal { }

            public class C
            {
                public void M(Dog dog)
                {
                    if ({|DL3016:dog is Animal|})
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
    public async Task NullableSubtypeToBase_WithoutVariable_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConcreteImplementsInterface_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            using System.Collections.Generic;

            public class C
            {
                public void M(List<int> list)
                {
                    if ({|DL3016:list is IEnumerable<int>|})
                    {
                        System.Console.WriteLine(list);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LocalVariable_AssignedFromProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class Node
            {
                public Node Parent { get; set; } = null!;
            }

            public class C
            {
                public void M(Node node)
                {
                    Node parent = node.Parent;
                    if ({|DL3016:parent is Node|})
                    {
                        System.Console.WriteLine(parent);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StandaloneExpression_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public void M(string str)
                {
                    var b = {|DL3016:str is string|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReturnExpression_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public bool M(string str)
                {
                    return {|DL3016:str is string|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PropertyAccess_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public string Name { get; set; } = "";

                public void M()
                {
                    if ({|DL3016:this.Name is string|})
                    {
                        System.Console.WriteLine(this.Name);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleDiagnosticsInOneFile_ReportsAll()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public void M(string str, int c)
                {
                    if ({|DL3016:str is string|})
                    {
                        System.Console.WriteLine(str);
                    }

                    if ({|DL3016:c is int|})
                    {
                        System.Console.WriteLine(c);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NarrowingTypeCheck_NoDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public void M(object obj)
                {
                    if (obj is string)
                    {
                        System.Console.WriteLine(obj);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BaseToDerived_NoDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class Animal { }
            public class Dog : Animal { }

            public class C
            {
                public void M(Animal animal)
                {
                    if (animal is Dog)
                    {
                        System.Console.WriteLine(animal);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullableReferenceType_IsTypeWithDeclaration_NoDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable enable
            public class C
            {
                public void M(string? str)
                {
                    if (str is string s)
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
    public async Task NullableSubtypeToBase_WithDeclaration_NoDiagnostic()
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
                    if (dog is Animal a)
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
            #nullable enable
            using System;
            using System.Linq;
            using System.Linq.Expressions;

            public class C
            {
                public void M(IQueryable<string> q)
                {
                    q.Where(s => s is string);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullableDisabled_ReferenceType_NoDiagnostic()
    {
        var test = CreateTest(
            """
            #nullable disable
            public class C
            {
                public void M(string str)
                {
                    if (str is string)
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
    public async Task NullableEnabled_InOtherwiseDisabledProject_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
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
            """
        );

        await test.RunAsync();
    }
}
