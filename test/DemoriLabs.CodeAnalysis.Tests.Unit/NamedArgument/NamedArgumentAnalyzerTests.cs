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
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<NamedArgumentAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task BoolLiteral_True_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:true|});
                private static void Foo(bool enabled) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BoolLiteral_False_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:false|});
                private static void Foo(bool enabled) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:null|});
                private static void Foo(string? value) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NumericLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:42|});
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StringLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:"hello"|});
                private static void Foo(string message) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CharLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:'x'|});
                private static void Foo(char separator) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BareDefault_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:default|});
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DefaultWithExplicitType_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:default(int)|});
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VariableNameMatchesParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var count = 5;
                    Foo(count);
                }
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task VariableNameDiffersFromParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var total = 5;
                    Foo({|DL3001:total|});
                }
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleArgumentsMixedNames_ReportsMismatches()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var name = "Alice";
                    var years = 30;
                    Foo(name, {|DL3001:years|});
                }
                private static void Foo(string name, int age) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AlreadyNamed_BoolLiteral_NoDiagnostic()
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
    public async Task AlreadyNamed_Variable_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    var total = 5;
                    Foo(count: total);
                }
                private static void Foo(int count) { }
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
    public async Task ConstructorCall_BoolLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Widget
            {
                public Widget(bool isVisible) { }
            }

            public class C
            {
                public void M() => _ = new Widget({|DL3001:true|});
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorInitializer_NumericLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Base
            {
                public Base(int timeout) { }
            }

            public class Derived : Base
            {
                public Derived() : base({|DL3001:30|}) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThisConstructorInitializer_BoolLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Widget
            {
                public Widget(bool isVisible) { }
                public Widget() : this({|DL3001:true|}) { }
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
    public async Task InvocationExpression_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M() => Foo({|DL3001:GetCount()|});
                private static int GetCount() => 0;
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task UnderscorePrefixedField_NameMatches_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int _count = 0;
                public void M() => Foo(_count);
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task UnderscorePrefixedField_NameDiffers_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int _total = 0;
                public void M() => Foo({|DL3001:_total|});
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThisMemberAccess_NameMatches_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int count = 0;
                public void M() => Foo(this.count);
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThisMemberAccess_NameDiffers_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int total = 0;
                public void M() => Foo({|DL3001:this.total|});
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ObjectMemberAccess_NameMatches_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Other
            {
                public int Count;
            }

            public class C
            {
                public void M(Other other) => Foo(other.Count);
                private static void Foo(int count) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ObjectMemberAccess_NameDiffers_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Other
            {
                public int Total;
            }

            public class C
            {
                public void M(Other other) => Foo({|DL3001:other.Total|});
                private static void Foo(int count) { }
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
    public async Task RecordConstructor_LiteralArgument_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person(string Name, int Age);

            public class C
            {
                public void M()
                {
                    _ = new Person({|DL3001:"Alice"|}, {|DL3001:30|});
                }
            }
            """
        );

        await test.RunAsync();
    }
}
