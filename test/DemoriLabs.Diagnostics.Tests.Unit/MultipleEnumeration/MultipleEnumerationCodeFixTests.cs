using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.MultipleEnumeration;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.MultipleEnumeration;

// ReSharper disable MemberCanBeMadeStatic.Global
public class MultipleEnumerationCodeFixTests
{
    private static CSharpCodeFixTest<
        MultipleEnumerationAnalyzer,
        MultipleEnumerationCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<MultipleEnumerationAnalyzer, MultipleEnumerationCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task TwoLinqMethods_MaterializesWithToList()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var list = {|DL5001:items|}.ToList();
                    var array = {|DL5001:items|}.ToArray();
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var itemsList = items.ToList();
                    var list = itemsList.ToList();
                    var array = itemsList.ToArray();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ForEachAndLinq_MaterializesWithToList()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var count = {|DL5001:items|}.Count();
                    foreach (var x in {|DL5001:items|}) { }
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var itemsList = items.ToList();
                    var count = itemsList.Count();
                    foreach (var x in itemsList) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AddsLinqUsing_WhenMissing()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    foreach (var x in {|DL5001:items|}) { }
                    foreach (var y in {|DL5001:items|}) { }
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var itemsList = items.ToList();
                    foreach (var x in itemsList) { }
                    foreach (var y in itemsList) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
