using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.InvertIf;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.InvertIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class InvertIfToReduceNestingCodeFixTests
{
    private static CSharpCodeFixTest<
        InvertIfToReduceNestingAnalyzer,
        InvertIfToReduceNestingCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<InvertIfToReduceNestingAnalyzer, InvertIfToReduceNestingCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }
}
