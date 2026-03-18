using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.InvertIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.InvertIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class InvertIfToReduceNestingAnalyzerTests
{
    private static CSharpAnalyzerTest<InvertIfToReduceNestingAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<InvertIfToReduceNestingAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }
}
