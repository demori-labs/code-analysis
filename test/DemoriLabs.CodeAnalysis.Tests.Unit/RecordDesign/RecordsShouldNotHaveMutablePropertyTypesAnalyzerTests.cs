using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class RecordsShouldNotHaveMutablePropertyTypesAnalyzerTests
{
    private static CSharpAnalyzerTest<RecordsShouldNotHaveMutablePropertyTypesAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<RecordsShouldNotHaveMutablePropertyTypesAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task RecordWithListProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public record Person
            {
                public List<string> {|DL1002:Tags|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithDictionaryProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public record Config
            {
                public Dictionary<string, string> {|DL1002:Settings|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithHashSetProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public record Person
            {
                public HashSet<string> {|DL1002:Roles|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithArrayProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record Data
            {
                public int[] {|DL1002:Values|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithPositionalArrayParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record Data(int[] {|DL1002:Values|});
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithPositionalListParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public record Data(List<string> {|DL1002:Items|});
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithIReadOnlyListProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public record Person
            {
                public IReadOnlyList<string> Tags { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithImmutableListProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Immutable;

            public record Person
            {
                public ImmutableList<string> Tags { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithFrozenSetProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Frozen;

            public record Person
            {
                public FrozenSet<string> Tags { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithStringProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string Name { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithConcurrentDictionaryProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Concurrent;

            public record Cache
            {
                public ConcurrentDictionary<string, object> {|DL1002:Items|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithObservableCollectionProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.ObjectModel;

            public record ViewModel
            {
                public ObservableCollection<string> {|DL1002:Items|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RegularClassWithListProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class Person
            {
                public List<string> Tags { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithMultipleMutableProperties_ReportsMultipleDiagnostics()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public record Person
            {
                public List<string> {|DL1002:Tags|} { get; init; }
                public Dictionary<string, int> {|DL1002:Scores|} { get; init; }
                public string Name { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithCustomCollectionInheritingList_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class MyCollection<T> : List<T> { }

            public record Data
            {
                public MyCollection<string> {|DL1002:Items|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithCustomCollectionInheritingDictionary_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class LookupTable : Dictionary<string, int> { }

            public record Config
            {
                public LookupTable {|DL1002:Mappings|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordWithDeeplyNestedCustomCollection_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class BaseCollection<T> : List<T> { }
            public class SpecialCollection<T> : BaseCollection<T> { }

            public record Data
            {
                public SpecialCollection<string> {|DL1002:Items|} { get; init; }
            }
            """
        );

        await test.RunAsync();
    }
}
