using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class LogicalPatternAnalyzerTests
{
    private static CSharpAnalyzerTest<LogicalPatternAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<LogicalPatternAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task TwoEqualsOrChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x == 1 || x == 2|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThreeEqualsOrChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x == 1 || x == 2 || x == 3|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StringEqualsOrChain_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s == "a" || s == "b") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RangeCheck_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x >= 0 && x < 100|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RangeCheckExclusive_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x > 0 && x <= 50|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsAndChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x != 1 && x != 2|}) { }
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
            public class C
            {
                public void M(int x, int y)
                {
                    if (x == 1 || y == 2) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonConstantRightSide_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x, int y, int z)
                {
                    if (x == y || x == z) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MixedEqualityAndRelationalOr_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x == 1 || x > 10|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SingleComparison_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x == 1) { }
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
                public void M(IQueryable<int> q)
                {
                    q.Where(x => x == 1 || x == 2);
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AlreadyUsingPatternMatching_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is 1 or 2 or 3) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MixedAndWithNonRelational_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x, bool flag)
                {
                    if (x > 0 && flag) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ThreeLeafAndChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x >= 0 && x < 100 && x != 50|}) { }
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
                public void M(int x, int y)
                {
                    if ({|DL3005:x == 1 || x == 2|}) { }
                    if ({|DL3005:y >= 0 && y < 100|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullableEqualsNullOrEqualsConstant_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id == null || id == 0|})
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
    public async Task HasValueNegationOrEqualsConstant_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int? M(int? id)
                {
                    if ({|DL3005:!id.HasValue || id == 0|})
                        return null;
                    return id;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullableEqualityOrRelational_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id == null || id > 10|})
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
    public async Task HasValueAndNotEqualsConstant_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int? M(int? id)
                {
                    if ({|DL3005:id.HasValue && id != 0|})
                        return id;
                    return null;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullableNotEqualsAndChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id != null && id != 0|})
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
    public async Task CustomTypeWithHasValueProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Option
            {
                public bool HasValue { get; set; }
                public int Value { get; set; }
            }

            public class C
            {
                public void M(Option opt)
                {
                    if (!opt.HasValue || opt.Value == 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CustomTypeWithHasValuePropertyAnd_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Option
            {
                public bool HasValue { get; set; }
                public int Value { get; set; }
            }

            public class C
            {
                public void M(Option opt)
                {
                    if (opt.HasValue && opt.Value != 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CustomTypeWithHasValueOrNull_NoDiagnostic()
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
                    if (!opt.HasValue || opt == null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MixedNotEqualsAndRelational_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id != null && id > 0|})
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
    public async Task NewTest()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:!id.HasValue && id > 0|})
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
    public async Task IsPatternAndChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id is not null && id is not 0|})
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
    public async Task IsPatternAndRelational_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id is not null && id is > 0|})
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
    public async Task IsPatternOrChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id is null || id is 0|})
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
    public async Task IsPatternRelationalAndChain_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3005:x is > 0 && x is < 100|})
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
    public async Task IsPatternDifferentVariables_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id, int? other)
                {
                    if (id is not null && other is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullableValueAccessAndNullCheck_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id != null && id.Value > 0|})
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
    public async Task NullableHasValueAndValueAccess_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id.HasValue && id.Value > 0|})
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
    public async Task NullableIsNotNullAndValueAccess_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3005:id is not null && id.Value > 0|})
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
    public async Task NonNullableValueProperty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Option
            {
                public int Value { get; set; }
            }

            public class C
            {
                public void M(Option? opt)
                {
                    if (opt != null && opt.Value > 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
