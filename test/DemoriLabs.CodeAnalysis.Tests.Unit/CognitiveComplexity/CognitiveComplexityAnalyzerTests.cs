using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CognitiveComplexity;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.CognitiveComplexity;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class CognitiveComplexityAnalyzerTests
{
    private static CSharpAnalyzerTest<CognitiveComplexityAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        int? moderateThreshold = null,
        int? elevatedThreshold = null
    )
    {
        var test = new CSharpAnalyzerTest<CognitiveComplexityAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        if (moderateThreshold is null && elevatedThreshold is null)
            return test;

        var moderate = moderateThreshold ?? CognitiveComplexityAnalyzer.DefaultModerateThreshold;
        var elevated = elevatedThreshold ?? CognitiveComplexityAnalyzer.DefaultElevatedThreshold;

        test.TestState.AnalyzerConfigFiles.Add(
            (
                "/.config",
                $"""
                is_global = true
                dotnet_diagnostic.DL4001.cognitive_complexity_moderate_threshold = {moderate}
                dotnet_diagnostic.DL4001.cognitive_complexity_elevated_threshold = {elevated}
                """
            )
        );

        return test;
    }
}
