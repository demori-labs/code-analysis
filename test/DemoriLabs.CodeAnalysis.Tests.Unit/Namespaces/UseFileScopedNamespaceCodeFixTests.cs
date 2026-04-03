using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.Namespaces;
using DemoriLabs.CodeAnalysis.Namespaces;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.Namespaces;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UseFileScopedNamespaceCodeFixTests
{
    private static CSharpCodeFixTest<
        UseFileScopedNamespaceAnalyzer,
        UseFileScopedNamespaceCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<UseFileScopedNamespaceAnalyzer, UseFileScopedNamespaceCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task SingleBlockNamespace_ConvertsToFileScoped()
    {
        var test = CreateTest(
            """
            namespace {|DL3019:MyApp|}
            {
                public class C { }
            }
            """,
            """
            namespace MyApp;

            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleBlockNamespace_MultipleMembers_ConvertsToFileScoped()
    {
        var test = CreateTest(
            """
            namespace {|DL3019:MyApp|}
            {
                public class A { }

                public class B { }

                public enum E { X, Y }
            }
            """,
            """
            namespace MyApp;

            public class A { }

            public class B { }

            public enum E { X, Y }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleSameNameBlockNamespaces_MergesIntoFileScoped()
    {
        var test = CreateTest(
            """
            namespace {|DL3019:MyApp|}
            {
                public class A { }
            }

            namespace {|DL3019:MyApp|}
            {
                public class B { }
            }
            """,
            """
            namespace MyApp;

            public class A { }

            public class B { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedNamespace_FlattensToFileScoped()
    {
        var test = CreateTest(
            """
            namespace {|DL3019:MyApp|}
            {
                namespace Inner
                {
                    public class C { }
                }
            }
            """,
            """
            namespace MyApp.Inner;

            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DeeplyNestedNamespace_FlattensToFileScoped()
    {
        var test = CreateTest(
            """
            namespace {|DL3019:A|}
            {
                namespace B
                {
                    namespace C
                    {
                        public class X { }
                    }
                }
            }
            """,
            """
            namespace A.B.C;

            public class X { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesUsings()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;

            namespace {|DL3019:MyApp|}
            {
                public class C
                {
                    public List<int> Items { get; } = new();
                }
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            namespace MyApp;

            public class C
            {
                public List<int> Items { get; } = new();
            }
            """
        );

        await test.RunAsync();
    }
}
