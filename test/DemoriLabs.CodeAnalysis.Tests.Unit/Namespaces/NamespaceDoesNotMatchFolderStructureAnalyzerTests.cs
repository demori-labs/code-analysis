using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Namespaces;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.Namespaces;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class NamespaceDoesNotMatchFolderStructureAnalyzerTests
{
    private static CSharpAnalyzerTest<NamespaceDoesNotMatchFolderStructureAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
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

        var test = new CSharpAnalyzerTest<NamespaceDoesNotMatchFolderStructureAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", config));

        if (filePath is not null)
        {
            test.TestState.Sources.Add((filePath, source));
        }
        else
        {
            test.TestCode = source;
        }

        return test;
    }
}
