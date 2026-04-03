namespace DemoriLabs.CodeAnalysis.Tests.Unit.Namespaces;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class NamespaceDoesNotMatchFolderStructureAnalyzerTests
{
    [Test]
    public async Task FileScopedNamespace_WrongNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:WrongNamespace|};

            public class C { }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BlockScopedNamespace_WrongNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:WrongNamespace|}
            {
                public class C { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FileInSubfolder_WrongNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:TestNamespace|};

            public class C { }
            """,
            filePath: "/0/Models/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FileInSubfolder_PartiallyCorrectNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:TestNamespace.Services|};

            public class C { }
            """,
            filePath: "/0/Models/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task FileInDeepSubfolder_WrongNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:TestNamespace|};

            public class C { }
            """,
            filePath: "/0/Models/Entities/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleBlockNamespaces_BothWrong_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:Wrong1|}
            {
                public class A { }
            }

            namespace {|DL3018:Wrong2|}
            {
                public class B { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedBlockNamespace_WrongOuter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:Wrong|}
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
    public async Task FileInSubfolder_ExtraSegment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:TestNamespace.Models.Extra|};

            public class C { }
            """,
            filePath: "/0/Models/Test0.cs"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ProjectDirWithTrailingSlash_WrongNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:Wrong|};

            public class C { }
            """,
            projectDir: "/0/"
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ProjectDirWithoutTrailingSlash_WrongNamespace_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            namespace {|DL3018:Wrong|};

            public class C { }
            """,
            projectDir: "/0"
        );

        await test.RunAsync();
    }
}
