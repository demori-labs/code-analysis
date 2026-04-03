namespace DemoriLabs.CodeAnalysis.Tests.Unit.Namespaces;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class NamespaceDoesNotMatchFolderStructureAnalyzerTests
{
    [Test]
    public async Task FileScopedNamespace_CorrectNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace TestNamespace;

            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockScopedNamespace_CorrectNamespace_NoDiagnostic()
    {
        var test = CreateTest(
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
    public async Task FileInSubfolder_CorrectNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace TestNamespace.Models;

            public class C { }
            """,
            filePath: "/0/Models/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FileInDeepSubfolder_CorrectNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace TestNamespace.Models.Entities;

            public class C { }
            """,
            filePath: "/0/Models/Entities/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoNamespaceDeclaration_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GlobalNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            global using System;

            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedBlockNamespace_BothCorrect_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace TestNamespace
            {
                namespace Inner
                {
                    public class C { }
                }
            }
            """,
            filePath: "/0/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ProjectDirWithoutTrailingSlash_CorrectNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace TestNamespace;

            public class C { }
            """,
            projectDir: "/0"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DifferentRootNamespace_CorrectNamespace_NoDiagnostic()
    {
        var test = CreateTest(
            """
            namespace MyCompany.MyProject;

            public class C { }
            """,
            rootNamespace: "MyCompany.MyProject"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DifferentRootNamespace_WithSubfolder_NoDiagnostic()
    {
        var test = CreateTest(
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
