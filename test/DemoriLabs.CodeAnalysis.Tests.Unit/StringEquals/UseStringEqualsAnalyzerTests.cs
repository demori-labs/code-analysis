using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.StringEquals;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.StringEquals;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UseStringEqualsAnalyzerTests
{
    private static CSharpAnalyzerTest<UseStringEqualsAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<UseStringEqualsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task StringEqualsLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s == "hello"|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StringNotEqualsLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s != "hello"|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReversedOperandOrder_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:"hello" == s|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoStringVariables_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string a, string b)
                {
                    if ({|DL3017:a == b|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task TwoStringVariablesInequality_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string a, string b)
                {
                    if ({|DL3017:a != b|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsStringLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s is "hello"|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotStringLiteral_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s is not "hello"|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodReturnValue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    if ({|DL3017:GetString() == "hello"|}) { }
                }

                private string GetString() => "hello";
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ChainedWithLogicalOr_EachLeafFlagged()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3017:s == "a"|} || {|DL3017:s == "b"|}) { }
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
                public C(string s)
                {
                    if ({|DL3017:s == "hello"|}) { }
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
                private string _s = "";
                public bool IsHello => {|DL3017:_s == "hello"|};
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
                public void M(List<string> items)
                {
                    items.Where(s => {|DL3017:s == "hello"|});
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
                public void M(string s)
                {
                    bool Check() => {|DL3017:s == "hello"|};
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
                public int M(string s)
                {
                    return {|DL3017:s == "hello"|} ? 1 : 0;
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
                public void M(string a, string b)
                {
                    if ({|DL3017:a == "x"|}) { }
                    if ({|DL3017:b != "y"|}) { }
                    if ({|DL3017:a is "z"|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsNull_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if (s == null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsNull_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if (s != null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsDefault_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if (s == default) { }
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
                public void M(string? s)
                {
                    if (s is null) { }
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
                public void M(string? s)
                {
                    if (s is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BothSidesConstant_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    if ("a" == "b") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BothSidesConstantWithConstVariable_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    const string a = "x";
                    if (a == "y") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IntComparison_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int n)
                {
                    if (n == 42) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ObjectComparison_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object a, object b)
                {
                    if (a == b) { }
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
                public void M(IQueryable<string> q)
                {
                    q.Where(s => s == "hello");
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
                    Expression<Func<string, bool>> expr = s => s == "hello";
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsOrPattern_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s is "a" or "b") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsAndNotPattern_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s is not "a" and not "b") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullEqualsString_Reversed_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if (null == s) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
