using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NullSimplification;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NullCoalescingAssignmentAnalyzerTests
{
    private static CSharpAnalyzerTest<NullCoalescingAssignmentAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<NullCoalescingAssignmentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsNull_SimpleAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    {|DL3014:if|} (x == null)
                    {
                        x = new Foo();
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNull_Pattern_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    {|DL3014:if|} (x is null)
                    {
                        x = new Foo();
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsNull_BlockBody_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    {|DL3014:if|} (x == null)
                    {
                        x = new Foo();
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsNull_MethodCallValue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    {|DL3014:if|} (x == null)
                    {
                        x = GetDefault();
                    }
                }

                private Foo GetDefault() => new Foo();
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsNull_FieldAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                private Foo? _field;

                public void M()
                {
                    {|DL3014:if|} (_field == null)
                    {
                        _field = new Foo();
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsNull_PropertyAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                public void M(C obj)
                {
                    {|DL3014:if|} (obj.Prop == null)
                    {
                        obj.Prop = new Foo();
                    }
                }

                public Foo? Prop { get; set; }
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

            public class Foo { }

            public class C
            {
                public void M()
                {
                    Action<Foo?> a = x =>
                    {
                        {|DL3014:if|} (x == null)
                        {
                            x = new Foo();
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
            public class Foo { }

            public class C
            {
                public void M()
                {
                    void Inner(Foo? x)
                    {
                        {|DL3014:if|} (x == null)
                        {
                            x = new Foo();
                        }
                    }
                }
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
            public class Foo { }

            public class C
            {
                public void M(Foo? x, Foo y)
                {
                    if (x == null)
                    {
                        y = new Foo();
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
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    if (x == null)
                    {
                        x = new Foo();
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
    public async Task MultipleStatements_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    if (x == null)
                    {
                        x = new Foo();
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
    public async Task NotNullCheck_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    if (x != null)
                    {
                        x = new Foo();
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
                public void M()
                {
                    Expression<Action<string?>> expr = x =>
                        Assign(x == null ? "default" : x);
                }

                private void Assign(string value) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CompoundAssignment_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? x)
                {
                    if (x == null)
                    {
                        x += 1;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
