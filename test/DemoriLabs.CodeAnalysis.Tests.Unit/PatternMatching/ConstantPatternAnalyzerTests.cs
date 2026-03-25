using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ConstantPatternAnalyzerTests
{
    private static CSharpAnalyzerTest<ConstantPatternAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<ConstantPatternAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsNull_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o {|DL3003:== null|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsNull_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o {|DL3003:!= null|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullEqualsX_Reversed_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:null ==|} o) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNull_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNull_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if (o is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b {|DL3003:== true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b {|DL3003:== false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b {|DL3003:!= true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b {|DL3003:!= false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsIntLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(List<int> x)
                {
                    if (x.Count {|DL3003:== 42|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsIntLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x {|DL3003:!= 0|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReversedIntLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3003:42 ==|} x) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsStringLiteral_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s == "hello") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsEnumMember_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public enum Color { Red, Green, Blue }

            public class C
            {
                public void M(Color c)
                {
                    if (c {|DL3003:== Color.Red|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsEnumMember_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public enum Color { Red, Green, Blue }

            public class C
            {
                public void M(Color c)
                {
                    if (c {|DL3003:!= Color.Blue|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsCharLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(char ch)
                {
                    if (ch {|DL3003:== 'a'|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsConstField_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private const int MaxRetries = 3;

                public void M(int retries)
                {
                    if (retries {|DL3003:== MaxRetries|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoVariables_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int a, int b)
                {
                    if (a == b) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoConstants_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    if (1 == 1) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideExpressionTree_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq;
            using System.Linq.Expressions;

            public class C
            {
                public void M(IQueryable<string?> q)
                {
                    q.Where(s => s == null);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideExpressionTreeExplicit_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public void M()
                {
                    Expression<Func<string?, bool>> expr = s => s == null;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleDiagnosticsInSameMethod()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o, int x)
                {
                    if (o {|DL3003:== null|}) { }
                    if (x {|DL3003:!= 0|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InConstructor_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public C(object? o)
                {
                    if (o {|DL3003:== null|}) throw new System.ArgumentNullException();
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private object? _value;
                public bool HasValue => _value {|DL3003:!= null|};
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InLambda_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<string?> items)
                {
                    items.Where(s => s {|DL3003:!= null|});
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InLocalFunction_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    bool IsNull() => o {|DL3003:== null|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InTernary_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(object? o) => o {|DL3003:== null|} ? "null" : "not null";
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegativeIntLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x {|DL3003:== -1|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
