using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.NamedArgument;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.NamedArgument;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NamedArgumentAnalyzerTests
{
    private static CSharpAnalyzerTest<NamedArgumentAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        int? namedArgumentsThreshold = null
    )
    {
        var test = new CSharpAnalyzerTest<NamedArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        if (namedArgumentsThreshold.HasValue)
        {
            test.TestState.AnalyzerConfigFiles.Add(
                (
                    "/.editorconfig",
                    $"""
                    root = true

                    [*]
                    dotnet_diagnostic.DL3001.named_arguments_threshold = {namedArgumentsThreshold.Value}
                    """
                )
            );
        }

        return test;
    }

    [Test]
    public async Task SingleParam_BoolLiteral_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(true);
                private static void Foo(bool enabled) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_NullLiteral_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(null);
                private static void Foo(string? value) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_NumericLiteral_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(42);
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_StringLiteral_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo("hello");
                private static void Foo(string message) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_InterpolatedString_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var op = "+";
                    throw new System.Exception($"Operator '{op}' not supported.");
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_Default_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(default);
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_DefaultWithType_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(default(int));
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_VariableNameMismatch_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var total = 5;
                    Foo(total);
                }
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoParams_MismatchedName_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var years = 30;
                    Foo(name, years);
                }
                private static void Foo(string name, int age) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_InvocationExpression_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(GetCount());
                private static int GetCount() => 0;
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_ConditionalExpression_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var x = true;
                    Foo(x ? 1 : 2);
                }
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleParam_ObjectCreation_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M() => Foo(new object());
                private static void Foo(object value) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AlreadyNamed_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(enabled: true);
                private static void Foo(bool enabled) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OutArgument_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    int result;
                    TryGet(out result);
                }
                private static bool TryGet(out int value) { value = 0; return true; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RefArgument_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    int x = 0;
                    Increment(ref x);
                }
                private static void Increment(ref int value) { value++; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParamsArgument_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo(1, 2, 3);
                private static void Foo(params int[] values) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Indexer_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var arr = new int[5];
                    _ = arr[0];
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NamedArgumentAttribute_VariableNameMatches_StillReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M()
                {
                    var count = 5;
                    Foo({|DL3001:count|});
                }
                private static void Foo([NamedArgument] int count) { }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(NamedArgumentAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task NamedArgumentAttribute_AlreadyNamed_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M()
                {
                    Foo(count: 5);
                }
                private static void Foo([NamedArgument] int count) { }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(NamedArgumentAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task NamedArgumentAttribute_OnlyAnnotatedParameterFlagged()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    Foo(name, {|DL3001:5|});
                }
                private static void Foo(string name, [NamedArgument] int count) { }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(NamedArgumentAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task NamedArgumentAttribute_RecordPrimaryConstructor_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public record Person([NamedArgument] string Name, [NamedArgument] int Age);

            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    _ = new Person({|DL3001:name|}, {|DL3001:30|});
                }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(NamedArgumentAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task NamedArgumentAttribute_RecordPrimaryConstructor_AlreadyNamed_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public record Person([NamedArgument] string Name, [NamedArgument] int Age);

            public class C
            {
                public void M()
                {
                    _ = new Person(Name: "Alice", Age: 30);
                }
            }
            """
        );

        test.TestState.AdditionalReferences.Add(typeof(NamedArgumentAttribute).Assembly);
        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_ExceedsDefault_MismatchedNames_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var n = "Alice";
                    var a = 30;
                    var e = "a@b.com";
                    Foo({|DL3001:n|}, {|DL3001:a|}, {|DL3001:e|});
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_ExceedsDefault_MatchingNames_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var age = 30;
                    var email = "a@b.com";
                    Foo(name, age, email);
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_ExceedsDefault_LiteralsReported()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    Foo({|DL3001:"Alice"|}, {|DL3001:30|}, {|DL3001:"a@b.com"|});
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_ExceedsDefault_MixedMatchAndMismatch_OnlyMismatchReported()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var years = 30;
                    var email = "a@b.com";
                    Foo(name, {|DL3001:years|}, email);
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_AtDefault_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var n = "Alice";
                    var a = 30;
                    Foo(n, a);
                }
                private static void Foo(string name, int age) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_CustomValue_ReportsAboveThreshold()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var n = "Alice";
                    var a = 30;
                    var e = "a@b.com";
                    var p = "123";
                    Foo({|DL3001:n|}, {|DL3001:a|}, {|DL3001:e|}, {|DL3001:p|});
                }
                private static void Foo(string name, int age, string email, string phone) { }
            }
            """,
            namedArgumentsThreshold: 3
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_CustomValue_NoDiagnosticAtThreshold()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var n = "Alice";
                    var a = 30;
                    var e = "a@b.com";
                    Foo(n, a, e);
                }
                private static void Foo(string name, int age, string email) { }
            }
            """,
            namedArgumentsThreshold: 3
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_AlreadyNamed_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    Foo(name: "Alice", age: 30, email: "a@b.com");
                }
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_ExceedsDefault_ConstructorCall_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Widget
            {
                public Widget(string name, int width, int height) { }
            }

            public class C
            {
                public void M()
                {
                    var n = "btn";
                    var w = 100;
                    var h = 200;
                    _ = new Widget({|DL3001:n|}, {|DL3001:w|}, {|DL3001:h|});
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_UnderscorePrefixedField_MatchesParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private string _name = "";
                private int _age = 0;
                private string _email = "";
                public void M() => Foo(_name, _age, _email);
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_MemberAccess_MatchesParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Other
            {
                public string Name = "";
                public int Age;
                public string Email = "";
            }

            public class C
            {
                public void M(Other o) => Foo(o.Name, o.Age, o.Email);
                private static void Foo(string name, int age, string email) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionMethod_ThisParameterNotCounted()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M()
                {
                    var items = new List<string>();
                    _ = items.Where(x => x.Length > 0);
                }
            }
            """,
            namedArgumentsThreshold: 1
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionMethod_ExceedsThreshold_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Extensions
            {
                public static void DoWork(this string s, int count, bool verbose) { }
            }

            public class C
            {
                public void M()
                {
                    "hello".DoWork({|DL3001:5|}, {|DL3001:true|});
                }
            }
            """,
            namedArgumentsThreshold: 1
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LambdaArgument_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M()
                {
                    var items = new List<int>();
                    _ = items.Where(x => x > 0);
                    _ = items.Select(x => x.ToString());
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AnonymousMethodArgument_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    var items = new List<int>();
                    items.ForEach(delegate(int x) { });
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IQueryable_Where_LambdaPredicate_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class Document
            {
                public int Id { get; set; }
                public bool ManuallyUploaded { get; set; }
            }

            public class C
            {
                public void M()
                {
                    var ids = new List<int> { 1, 2 };
                    var documents = new List<Document>().AsQueryable()
                        .Where(d => ids.Contains(d.Id))
                        .ToList();

                    var filtered = documents
                        .Where(document => !document.ManuallyUploaded)
                        .ToList();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IEnumerable_ChainedWhere_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class Document
            {
                public string Status { get; set; } = "";
                public DateTime UploadedAt { get; set; }
            }

            public class C
            {
                public void M()
                {
                    var documents = new List<Document>();
                    var result = documents
                        .Where(d => d.Status != "Uploaded")
                        .Where(d => DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)) > d.UploadedAt)
                        .ToList();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_ThisParameterNotCounted()
    {
        var test = CreateTest(
            """
            public static class StringExtensions
            {
                extension(string s)
                {
                    public bool IsLongerThan(int length) => s.Length > length;
                }
            }

            public class C
            {
                public void M()
                {
                    _ = "hello".IsLongerThan(3);
                }
            }
            """,
            namedArgumentsThreshold: 1
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_ExceedsThreshold_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public static class StringExtensions
            {
                extension(string s)
                {
                    public void DoWork(int count, bool verbose) { }
                }
            }

            public class C
            {
                public void M()
                {
                    "hello".DoWork({|DL3001:5|}, {|DL3001:true|});
                }
            }
            """,
            namedArgumentsThreshold: 1
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExtensionBlock_MatchingNames_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public static class StringExtensions
            {
                extension(string s)
                {
                    public void DoWork(int count, bool verbose) { }
                }
            }

            public class C
            {
                public void M()
                {
                    var count = 5;
                    var verbose = true;
                    "hello".DoWork(count, verbose);
                }
            }
            """,
            namedArgumentsThreshold: 1
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParamsMethod_ParamsNotCounted_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Logger
            {
                public static void LogDebug(this object logger, string? message, params object?[] args) { }
            }

            public class C
            {
                private readonly object _logger = new();

                public void M()
                {
                    _logger.LogDebug("Start processing message: {Message}", "test");
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParamsMethod_OnlyRegularParamsCounted()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    Foo("format", "a", "b", "c");
                }
                private static void Foo(string format, params object[] args) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OutParam_NotCounted()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    Parse("123", "en", out var result);
                }
                private static bool Parse(string input, string culture, out int result) { result = 0; return true; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EnumParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System.IO;

            public class C
            {
                public void M()
                {
                    var path = "./file.txt";
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OptionalParams_NotCounted_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public static class ServiceCollectionExtensions
            {
                public static void AddDbContext<T>(
                    this object services,
                    Action<object>? optionsAction = null,
                    int contextLifetime = 0,
                    int optionsLifetime = 0) { }
            }

            public class C
            {
                public void M()
                {
                    var services = new object();
                    services.AddDbContext<object>(options => { });
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OptionalParams_ExplicitlyPassed_AboveThreshold_ReportsMismatch()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    Foo({|DL3001:"hello"|}, {|DL3001:99|}, {|DL3001:"a@b.com"|}, {|DL3001:5|});
                }
                private static void Foo(string name, int age, string email, int limit = 10) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ArgumentName_PrefixMatchesWithTypeSuffix_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M()
                {
                    Action<string> options = _ => { };
                    Func<int> count = () => 0;
                    var name = "test";
                    Foo(options, count, name);
                }
                private static void Foo(Action<string> optionsAction, Func<int> countFunc, string name) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ArgumentName_PrefixDoesNotMatchType_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M()
                {
                    Action<string> options = _ => { };
                    var count = 5;
                    var name = "test";
                    Foo(options, {|DL3001:count|}, name);
                }
                private static void Foo(Action<string> optionsAction, int countThreshold, string name) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ArgumentName_NoMatch_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var cfg = new object();
                    var n = "test";
                    var x = 5;
                    Foo({|DL3001:cfg|}, {|DL3001:n|}, {|DL3001:x|});
                }
                private static void Foo(object value, string name, int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExpressionTree_LambdaWithLiteral_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public void M()
                {
                    Expression<Func<bool>> expr = () => Foo(true);
                }
                private static bool Foo(bool enabled) => enabled;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExpressionTree_MethodCallInsideLambda_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public void M()
                {
                    Expression<Func<bool>> expr = () => Matches(42);
                }
                private static bool Matches(int threshold) => threshold > 0;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExpressionTree_VariableNameMismatch_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public void M()
                {
                    var total = 5;
                    Expression<Func<bool>> expr = () => Foo(total);
                }
                private static bool Foo(int count) => count > 0;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExpressionTree_NestedLambda_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public void M()
                {
                    Expression<Func<Func<bool>>> expr = () => () => Foo(true);
                }
                private static bool Foo(bool enabled) => enabled;
            }
            """
        );

        await test.RunAsync();
    }
}
