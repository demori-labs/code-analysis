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
                    if ({|DL3003:o == null|}) { }
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
                    if ({|DL3003:o != null|}) { }
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
                    if ({|DL3003:null == o|}) { }
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
                    if ({|DL3003:b == true|}) { }
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
                    if ({|DL3003:b == false|}) { }
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
                    if ({|DL3003:b != true|}) { }
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
                    if ({|DL3003:b != false|}) { }
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
                    if ({|DL3003:x.Count == 42|}) { }
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
                    if ({|DL3003:x != 0|}) { }
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
                    if ({|DL3003:42 == x|}) { }
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
                    if ({|DL3003:c == Color.Red|}) { }
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
                    if ({|DL3003:c != Color.Blue|}) { }
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
                    if ({|DL3003:ch == 'a'|}) { }
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
                    if ({|DL3003:retries == MaxRetries|}) { }
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
                    if ({|DL3003:o == null|}) { }
                    if ({|DL3003:x != 0|}) { }
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
                    if ({|DL3003:o == null|}) throw new System.ArgumentNullException();
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
                public bool HasValue => {|DL3003:_value != null|};
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
                    items.Where(s => {|DL3003:s != null|});
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
                    bool IsNull() => {|DL3003:o == null|};
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
                public string M(object? o) => {|DL3003:o == null|} ? "null" : "not null";
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
                    if ({|DL3003:x == -1|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsInLogicalOrChainSameVariable_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id == null || id == 0)
                    {
                        return;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsInLogicalOrChainWithHasValue_NoDiagnostic()
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
    public async Task EqualsInLogicalOrChainDifferentVariables_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x, int y)
                {
                    if ({|DL3003:x == 5|} || y > 3)
                    {
                        return;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsInLogicalOrChainMixedEqualityAndRelational_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id == null || id > 10)
                    {
                        return;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsInLogicalAndChainSameVariable_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x != 1 && x != 2)
                    {
                        return;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsInLogicalAndChainWithHasValue_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id.HasValue && id != 0)
                    {
                        return;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueEqualsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:id.HasValue == false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueEqualsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:id.HasValue == true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueNotEqualsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:id.HasValue != true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueNotEqualsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:id.HasValue != false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnLocalVariable_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    int? x = 5;
                    if ({|DL3003:x.HasValue == false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int? Value { get; set; }

                public void M()
                {
                    if ({|DL3003:Value.HasValue == true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnField_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int? _id;

                public void M()
                {
                    if ({|DL3003:_id.HasValue != true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnNestedPropertyAccess_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Inner { public int? Id { get; set; } }

            public class C
            {
                public void M(Inner inner)
                {
                    if ({|DL3003:inner.Id.HasValue == true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnCustomType_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class Option
            {
                public bool HasValue { get; set; }
            }

            public class C
            {
                public void M(Option opt)
                {
                    if ({|DL3003:opt.HasValue == true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullNotEqualsReversed_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:null != o|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WrappedComparisonEqualsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o == null) == false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WrappedComparisonEqualsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o == null) == true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WrappedNotEqualsComparisonEqualsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o != null) == false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ComparisonIsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o == null) is false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ComparisonIsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o == null) is true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsComparisonIsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o != null) is false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNullIsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is null) is false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNullIsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is not null) is false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNullIsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is null) is true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNullIsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is not null) is true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_IsNullIsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id == null) is false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ComparisonNotEqualsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o == null) != true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsComparisonEqualsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o != null) == true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsComparisonIsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o != null) is true|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_ComparisonEqualsFalse_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id == null) == false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_IsNullIsTrue_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id is null) is false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedNegatedHasValueWrapped_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(!(id.HasValue) is true) == false|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
