using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NullSimplification;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NullConditionalAssignmentAnalyzerTests
{
    private static CSharpAnalyzerTest<NullConditionalAssignmentAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<NullConditionalAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task IsNotNull_PropertyAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x is not null)
                    {
                        x.Prop = 42;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsNull_PropertyAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x != null)
                    {
                        x.Prop = 42;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNull_MethodCall_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void DoWork(int value) { }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x is not null)
                    {
                        x.DoWork(5);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsNull_BlockBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x != null)
                    {
                        x.Prop = 42;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNull_CompoundAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Count { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x is not null)
                    {
                        x.Count += 1;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNull_NestedMemberAccess_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Inner
            {
                public int Prop { get; set; }
            }

            public class C
            {
                public Inner Inner { get; } = new();
            }

            public class Test
            {
                public void M(C? x)
                {
                    {|DL3015:if|} (x is not null)
                    {
                        x.Inner.Prop = 42;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideLambda_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M()
                {
                    Action<C?> a = x =>
                    {
                        {|DL3015:if|} (x is not null)
                        {
                            x.Prop = 42;
                        }
                    };
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideLocalFunction_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M()
                {
                    void Inner(C? x)
                    {
                        {|DL3015:if|} (x is not null)
                        {
                            x.Prop = 42;
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Increment_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Count { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    if (x is not null)
                    {
                        x.Count++;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Decrement_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Count { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    if (x is not null)
                    {
                        x.Count--;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleStatements_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
                public int Other { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    if (x != null)
                    {
                        x.Prop = 1;
                        x.Other = 2;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasElse_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    if (x != null)
                    {
                        x.Prop = 42;
                    }
                    else
                    {
                        DoStuff();
                    }
                }

                private void DoStuff() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DifferentVariable_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x, C y)
                {
                    if (x != null)
                    {
                        y.Prop = 42;
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

            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M()
                {
                    Expression<Action<C?>> expr = x =>
                        Assign(x != null ? 42 : 0);
                }

                private void Assign(int value) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsNull_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Prop { get; set; }
            }

            public class Test
            {
                public void M(C? x)
                {
                    if (x == null)
                    {
                        x.Prop = 42;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VariableAssignmentWithoutMemberAccess_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C { }

            public class Test
            {
                public void M(C? x)
                {
                    if (x != null)
                    {
                        x = new C();
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
