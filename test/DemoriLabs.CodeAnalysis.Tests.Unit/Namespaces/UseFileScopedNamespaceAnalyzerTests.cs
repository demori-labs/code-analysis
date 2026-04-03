using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Namespaces;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.Namespaces;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class UseFileScopedNamespaceAnalyzerTests
{
    private static CSharpAnalyzerTest<UseFileScopedNamespaceAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<UseFileScopedNamespaceAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task SingleBlockNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3019:MyApp|}
            {
                public class C { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleBlockNamespaces_SameName_ReportsDiagnosticOnEach()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedNamespace_OuterHasNoMembers_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DeeplyNestedNamespace_NoDirectMembers_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EmptyBlockNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3019:MyApp|}
            {
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
    public async Task MultipleBlockNamespaces_DifferentNames_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace MyApp
            {
                public class A { }
            }

            namespace OtherApp
            {
                public class B { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedNamespace_OuterHasDirectMembers_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace MyApp
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
