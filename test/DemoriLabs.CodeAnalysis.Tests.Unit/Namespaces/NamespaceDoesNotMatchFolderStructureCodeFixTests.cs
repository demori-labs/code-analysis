using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.Namespaces;
using DemoriLabs.CodeAnalysis.Namespaces;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.Namespaces;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NamespaceDoesNotMatchFolderStructureCodeFixTests
{
    private static CSharpCodeFixTest<
        NamespaceDoesNotMatchFolderStructureAnalyzer,
        NamespaceDoesNotMatchFolderStructureCodeFix,
        DefaultVerifier
    > CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource,
        string rootNamespace = "TestNamespace",
        string projectDir = "/0/",
        string? filePath = null
    )
    {
        var config = $"""
            is_global = true
            build_property.RootNamespace = {rootNamespace}
            build_property.ProjectDir = {projectDir}
            """;

        var test = new CSharpCodeFixTest<
            NamespaceDoesNotMatchFolderStructureAnalyzer,
            NamespaceDoesNotMatchFolderStructureCodeFix,
            DefaultVerifier
        >
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.globalconfig", config));

        if (filePath is not null)
        {
            test.TestState.Sources.Add((filePath, source));
            test.FixedState.Sources.Add((filePath, fixedSource));
        }
        else
        {
            test.TestCode = source;
            test.FixedCode = fixedSource;
        }

        return test;
    }

    [Test]
    public async Task FileScopedNamespace_FixesNamespace()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:WrongNamespace|};

            public class C { }
            """,
            """
            namespace TestNamespace;

            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockScopedNamespace_FixesNamespace()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:WrongNamespace|}
            {
                public class C { }
            }
            """,
            """
            namespace TestNamespace
            {
                public class C { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FileInSubfolder_FixesNamespace()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:TestNamespace|};

            public class C { }
            """,
            """
            namespace TestNamespace.Models;

            public class C { }
            """,
            filePath: "/0/Models/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FileInDeepSubfolder_FixesNamespace()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:TestNamespace|};

            public class C { }
            """,
            """
            namespace TestNamespace.Models.Entities;

            public class C { }
            """,
            filePath: "/0/Models/Entities/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DifferentRootNamespace_FixesNamespace()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:Wrong|};

            public class C { }
            """,
            """
            namespace MyCompany.MyProject.Services;

            public class C { }
            """,
            rootNamespace: "MyCompany.MyProject",
            filePath: "/0/Services/Test0.cs"
        );

        await test.RunAsync();
    }
}
