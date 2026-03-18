using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.MultipleEnumeration;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.MultipleEnumeration;

// ReSharper disable MemberCanBeMadeStatic.Global
public class MultipleEnumerationAnalyzerTests
{
    private static CSharpAnalyzerTest<MultipleEnumerationAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<MultipleEnumerationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task TwoForEachLoops_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ForEachAndLinqMethod_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoLinqMethods_ReportsDiagnostic()
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
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AnyThenFirst_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    if ({|DL5001:items|}.Any())
                    {
                        var first = {|DL5001:items|}.First();
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LocalVariable_TwoEnumerations_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M()
                {
                    IEnumerable<int> items = GetItems();
                    var count = {|DL5001:items|}.Count();
                    var list = {|DL5001:items|}.ToList();
                }

                private IEnumerable<int> GetItems() => [];
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThreeEnumerations_FlagsAll()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var a = {|DL5001:items|}.Any();
                    var b = {|DL5001:items|}.Count();
                    var c = {|DL5001:items|}.ToList();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DeferredThenMaterialized_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var filtered = {|DL5001:items|}.Where(x => x > 5).ToList();
                    var mapped = {|DL5001:items|}.Select(x => x * 2).ToArray();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LinqQuerySyntax_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var q = from x in {|DL5001:items|} select x;
                    foreach (var y in {|DL5001:items|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GetEnumerator_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var e = {|DL5001:items|}.GetEnumerator();
                    foreach (var x in {|DL5001:items|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonGenericIEnumerable_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable items)
                {
                    foreach (var x in {|DL5001:items|}) { }
                    foreach (var y in {|DL5001:items|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Constructor_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public C(IEnumerable<int> items)
                {
                    var count = {|DL5001:items|}.Count();
                    var list = {|DL5001:items|}.ToList();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleForEach_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    foreach (var x in items) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleLinqMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var list = items.ToList();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConcreteListType_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(List<int> items)
                {
                    var count = items.Count();
                    foreach (var x in items) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ArrayType_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Linq;

            public class C
            {
                public void M(int[] items)
                {
                    var count = items.Count();
                    foreach (var x in items) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IListType_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IList<int> items)
                {
                    var count = items.Count();
                    foreach (var x in items) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ChainedLinq_SingleEnumeration_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var result = items.Where(x => x > 5).Select(x => x * 2).ToList();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoEnumeration_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var other = items;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EnumerationInLambda_DoesNotAffectOuterScope()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> items)
                {
                    var list = items.ToList();
                    Action a = () => { foreach (var x in items) { } };
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DifferentVariables_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> a, IEnumerable<int> b)
                {
                    var x = a.ToList();
                    var y = b.ToArray();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LocalVarInferredAsConcrete_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M()
                {
                    var items = new List<int> { 1, 2, 3 };
                    var count = items.Count();
                    foreach (var x in items) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SuppressMultipleEnumeration_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([SuppressMultipleEnumeration] IEnumerable<int> items)
                {
                    var count = items.Count();
                    var list = items.ToList();
                }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(SuppressMultipleEnumerationAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task SuppressMultipleEnumeration_OnlyAffectsAnnotatedParameter()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M(
                    [SuppressMultipleEnumeration] IEnumerable<int> safe,
                    IEnumerable<int> items
                )
                {
                    var a = safe.Count();
                    var b = safe.ToList();
                    var c = {|DL5001:items|}.Count();
                    var d = {|DL5001:items|}.ToList();
                }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(SuppressMultipleEnumerationAttribute).Assembly);
        await test.RunAsync();
    }
}
