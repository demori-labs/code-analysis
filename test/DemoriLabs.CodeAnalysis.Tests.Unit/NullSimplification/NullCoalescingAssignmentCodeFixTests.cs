using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.NullSimplification;
using DemoriLabs.CodeAnalysis.NullSimplification;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NullSimplification;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NullCoalescingAssignmentCodeFixTests
{
    private static CSharpCodeFixTest<
        NullCoalescingAssignmentAnalyzer,
        NullCoalescingAssignmentCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<NullCoalescingAssignmentAnalyzer, NullCoalescingAssignmentCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsNull_ReplacesWithCoalescingAssignment()
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
            """,
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    x ??= new Foo();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNull_Pattern_ReplacesWithCoalescingAssignment()
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
            """,
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    x ??= new Foo();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockBody_ReplacesWithCoalescingAssignment()
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
            """,
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    x ??= new Foo();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodCallValue_PreservesValue()
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
            """,
            """
            public class Foo { }

            public class C
            {
                public void M(Foo? x)
                {
                    x ??= GetDefault();
                }

                private Foo GetDefault() => new Foo();
            }
            """
        );

        await test.RunAsync();
    }
}
