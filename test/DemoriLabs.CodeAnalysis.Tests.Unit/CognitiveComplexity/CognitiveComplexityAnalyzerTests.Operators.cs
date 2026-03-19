namespace DemoriLabs.CodeAnalysis.Tests.Unit.CognitiveComplexity;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class CognitiveComplexityAnalyzerTests
{
    [Test]
    public async Task Operators_LogicalSequences()
    {
        // a || b || c (+1) + a && !b && c && d (+1) + a && b && c (+1) = 3
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M1|}(bool a, bool b, bool c, bool d)
                {
                    var x = a || b || c;
                    var x1 = a && !b && c && d;
                    var x2 = !(a && b && c);
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Operators_MixedLogicalInCondition()
    {
        // if (+1) + && (+1) + || (+1) + && (+1) = 4
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M2|}(bool a, bool b, bool c, bool d, bool e, bool f)
                {
                    if (a
                        && b && c
                        || d || e
                        && f)
                    {
                    }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Operators_TraditionalNullCheck()
    {
        // if (+1) = 1
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M1|}(object obj)
                {
                    string str = null;
                    if (obj != null)
                        str = obj.ToString();
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Operators_NullConditionalNoIncrement()
    {
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void M2(object obj)
                {
                    var str = obj?.ToString();
                }
            }
            """
        );

        await test.RunAsync();
    }
}
