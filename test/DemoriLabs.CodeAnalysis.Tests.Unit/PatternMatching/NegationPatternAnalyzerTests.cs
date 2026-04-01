using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class NegationPatternAnalyzerTests
{
    private static CSharpAnalyzerTest<NegationPatternAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<NegationPatternAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task NegatedBoolVariable_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool flag)
                {
                    if ({|DL3004:!flag|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedMethodCall_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL3004:!string.IsNullOrEmpty(s)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedPropertyAccess_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool IsReady { get; set; }

                public void M()
                {
                    if ({|DL3004:!IsReady|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedBoolInReturn_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public bool M(bool flag) => {|DL3004:!flag|};
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedBoolInLambda_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(List<bool> items)
                {
                    items.Where(b => {|DL3004:!b|});
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedBoolInTernary_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string M(bool flag) => {|DL3004:!flag|} ? "no" : "yes";
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedIsType_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if ({|DL3004:!(o is string)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedIsNull_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3004:!(o is null)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedIsPattern_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3004:!(x is > 5)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedIsDeclarationPattern_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object o)
                {
                    if ({|DL3004:!(o is string s)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsFalse_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool flag)
                {
                    if (flag is false) { }
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
    public async Task NegatedNullableBool_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool? flag)
                {
                    var result = !flag;
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
                public void M(IQueryable<bool> q)
                {
                    q.Where(b => !b);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedLogicalAnd_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (!(a && b)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedLogicalOr_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (!(a || b)) { }
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
                public void M(bool a, bool b)
                {
                    if ({|DL3004:!a|}) { }
                    if ({|DL3004:!b|}) { }
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
                public void M(bool flag)
                {
                    bool Check() => {|DL3004:!flag|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedEqualsNull_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3004:!(o == null)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedNotEqualsNull_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3004:!(o != null)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedHasValueStandalone_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3004:!id.HasValue|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_NegatedEqualsNull_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3004:!(id == null)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueNegationInLogicalOrChain_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int? M(int? id)
                {
                    if (!id.HasValue || id == 0)
                        return null;
                    return id;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueNegationWithValueAccessInAndChain_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if (!id.HasValue || id.Value == 0)
                    {
                        return;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
