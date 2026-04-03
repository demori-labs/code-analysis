using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Namespaces;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.Namespaces;

// ReSharper disable MemberCanBeMadeStatic.Global
public class MultipleNamespacesInFileAnalyzerTests
{
    private static CSharpAnalyzerTest<MultipleNamespacesInFileAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<MultipleNamespacesInFileAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task TwoBlockNamespaces_DifferentNames_ReportsDiagnosticOnEach()
    {
        var test = CreateTest(
            """
            namespace {|DL3020:MyApp|}
            {
                public class A { }
            }

            namespace {|DL3020:OtherApp|}
            {
                public class B { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThreeBlockNamespaces_AllDifferent_ReportsDiagnosticOnEach()
    {
        var test = CreateTest(
            """
            namespace {|DL3020:A|}
            {
                public class X { }
            }

            namespace {|DL3020:B|}
            {
                public class Y { }
            }

            namespace {|DL3020:C|}
            {
                public class Z { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedNamespace_OuterHasDirectMembers_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3020:MyApp|}
            {
                public class A { }

                namespace Inner
                {
                    public class B { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoBlockNamespaces_SameName_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace MyApp
            {
                public class A { }
            }

            namespace MyApp
            {
                public class B { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleBlockNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace MyApp
            {
                public class C { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FileScopedNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace MyApp;

            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedNamespace_OuterHasNoMembers_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace MyApp
            {
                namespace Inner
                {
                    public class C { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C { }
            """
        );

        await test.RunAsync();
    }
}
