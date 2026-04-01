using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PatternMatching;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ConstantPatternCodeFixTests
{
    private static CSharpCodeFixTest<ConstantPatternAnalyzer, ConstantPatternCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        return new CSharpCodeFixTest<ConstantPatternAnalyzer, ConstantPatternCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsNull_FixesToIsNull()
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
            """,
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
    public async Task NotEqualsNull_FixesToIsNotNull()
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
            """,
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
    public async Task ReversedNull_FixesToIsNull()
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
            """,
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
    public async Task EqualsBoolTrue_FixesToIsTrue()
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
            """,
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b is true) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsTrue_FixesToIsNotTrue()
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
            """,
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b is not true) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsIntLiteral_FixesToIsLiteral()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3003:x == 42|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is 42) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsIntLiteral_FixesToIsNotLiteral()
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
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is not 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegativeIntLiteral_FixesToIsWithParentheses()
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
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is -1) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsEnumMember_FixesToIsEnumMember()
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
            """,
            """
            public enum Color { Red, Green, Blue }

            public class C
            {
                public void M(Color c)
                {
                    if (c is Color.Red) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WrappedComparisonEqualsFalse_FixesToIsNotNull()
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
            """,
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
    public async Task WrappedComparisonEqualsTrue_FixesToIsNull()
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
            """,
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
    public async Task WrappedNotEqualsEqualsFalse_FixesToIsNull()
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
            """,
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
    public async Task HasValueEqualsFalse_FixesToIsFalse()
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
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueNotEqualsTrue_FixesToIsNotTrue()
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
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task WrappedIsNullEqualsFalse_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is null) == false|}) { }
                }
            }
            """,
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
    public async Task WrappedIsNullEqualsTrue_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is null) == true|}) { }
                }
            }
            """,
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
    public async Task WrappedIsNotNullEqualsFalse_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is not null) == false|}) { }
                }
            }
            """,
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
    public async Task WrappedIsNotNullNotEqualsTrue_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:(o is not null) != true|}) { }
                }
            }
            """,
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
    public async Task WrappedIsNullEqualsFalse_ValueType_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id is null) == false|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ComparisonIsFalse_FixesToIsNotNull()
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
            """,
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
    public async Task ComparisonIsTrue_FixesToIsNull()
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
            """,
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
    public async Task NotEqualsComparisonIsFalse_FixesToIsNull()
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
            """,
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
    public async Task IsNullIsFalse_FixesToIsNotNull()
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
            """,
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
    public async Task IsNotNullIsFalse_FixesToIsNull()
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
            """,
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
    public async Task ComparisonNotEqualsTrue_FixesToIsNotNull()
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
            """,
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
    public async Task NotEqualsComparisonEqualsTrue_FixesToIsNotNull()
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
            """,
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
    public async Task NotEqualsComparisonIsTrue_FixesToIsNotNull()
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
            """,
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
    public async Task HasValueEqualsTrue_FixesToIsTrue()
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
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueNotEqualsFalse_FixesToIsNotFalse()
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
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullNotEqualsReversed_FixesToIsNotNull()
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
            """,
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
    public async Task ValueType_ComparisonEqualsFalse_FixesToIsNotNull()
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
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ValueType_IsNullIsFalse_FixesToIsNotNull()
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
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnLocalVariable_FixesToIsNull()
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
            """,
            """
            public class C
            {
                public void M()
                {
                    int? x = 5;
                    if (x is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnProperty_FixesToIsNotNull()
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
            """,
            """
            public class C
            {
                public int? Value { get; set; }

                public void M()
                {
                    if (Value is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnField_FixesToIsNull()
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
            """,
            """
            public class C
            {
                private int? _id;

                public void M()
                {
                    if (_id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnNestedPropertyAccess_FixesToIsNotNull()
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
            """,
            """
            public class Inner { public int? Id { get; set; } }

            public class C
            {
                public void M(Inner inner)
                {
                    if (inner.Id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueOnCustomType_DoesNotRewriteToNullCheck()
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
            """,
            """
            public class Option
            {
                public bool HasValue { get; set; }
            }

            public class C
            {
                public void M(Option opt)
                {
                    if (opt.HasValue is true) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueWrappedEqualsFalse_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id.HasValue) == false|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueWrappedEqualsTrue_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id.HasValue) == true|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueComparisonIsFalse_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id.HasValue == true) is false|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueComparisonIsTrue_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id.HasValue == true) is true|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task HasValueComparisonEqualsFalse_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(id.HasValue == true) == false|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedHasValueIsTrue_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(!id.HasValue) is true|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedHasValueIsFalse_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(!id.HasValue) is false|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedHasValueIsNotTrue_FixesToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(!id.HasValue) is not true|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedHasValueIsNotFalse_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? id)
                {
                    if ({|DL3003:(!id.HasValue) is not false|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NegatedBoolIsTrue_FixesToIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool flag)
                {
                    if ({|DL3003:(!flag) is true|}) { }
                }
            }
            """,
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
    public async Task NegatedBoolIsFalse_FixesToIsTrue()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool flag)
                {
                    if ({|DL3003:(!flag) is false|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(bool flag)
                {
                    if (flag is true) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NestedNegatedHasValueWrapped_FixesToIsNotNull()
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
            """,
            """
            public class C
            {
                public void M(int? id)
                {
                    if (id is not null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IntEqualsDefault_FixesToIsZero()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3003:x == default|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BoolEqualsDefault_FixesToIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if ({|DL3003:b == default|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b is false) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EnumEqualsDefault_FixesToIsZero()
    {
        var test = CreateTest(
            """
            public enum Status { Active, Inactive }

            public class C
            {
                public void M(Status s)
                {
                    if ({|DL3003:s == default|}) { }
                }
            }
            """,
            """
            public enum Status { Active, Inactive }

            public class C
            {
                public void M(Status s)
                {
                    if (s is 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ObjectEqualsDefault_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:o == default|}) { }
                }
            }
            """,
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
    public async Task NullableIntEqualsDefault_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int? x)
                {
                    if ({|DL3003:x == default|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int? x)
                {
                    if (x is null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IntNotEqualsDefault_FixesToIsNotZero()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3003:x != default|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is not 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task DoubleEqualsDefault_FixesToIsZero()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(double d)
                {
                    if ({|DL3003:d == default|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(double d)
                {
                    if (d is 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IntEqualsDefaultOfT_FixesToIsZero()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3003:x == default(int)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ObjectEqualsDefaultOfT_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:o == default(object)|}) { }
                }
            }
            """,
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
    public async Task IntIsDefaultOfT_FixesToIsZero()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3003:x is default(int)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ObjectIsDefaultOfT_FixesToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? o)
                {
                    if ({|DL3003:o is default(object)|}) { }
                }
            }
            """,
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
    public async Task IntIsNotDefaultOfT_FixesToIsNotZero()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if ({|DL3003:x is not default(int)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(int x)
                {
                    if (x is not 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task BoolIsDefaultOfT_FixesToIsFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool b)
                {
                    if ({|DL3003:b is default(bool)|}) { }
                }
            }
            """,
            """
            public class C
            {
                public void M(bool b)
                {
                    if (b is false) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EnumIsDefaultOfT_FixesToIsZero()
    {
        var test = CreateTest(
            """
            public enum Status { Active, Inactive }

            public class C
            {
                public void M(Status s)
                {
                    if ({|DL3003:s is default(Status)|}) { }
                }
            }
            """,
            """
            public enum Status { Active, Inactive }

            public class C
            {
                public void M(Status s)
                {
                    if (s is 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
