namespace DemoriLabs.CodeAnalysis.Tests.Unit.CognitiveComplexity;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class CognitiveComplexityAnalyzerTests
{
    [Test]
    public async Task ControlFlow_IfElse()
    {
        // if (+1) else (+1) = 2
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M1|}(bool b)
                {
                    if (b)
                        Console.WriteLine();
                    else
                        Console.WriteLine();
                }
            }
            """,
            moderateThreshold: 1,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ControlFlow_NestedIfElseIfElse()
    {
        // if (+1) > if (+2, N=1) + else if (+1) + else (+1) = 5
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M2|}(bool b)
                {
                    if (true)
                    {
                        if (b)
                            Console.WriteLine();
                        else if (!b)
                            Console.WriteLine();
                        else
                            Console.WriteLine();
                    }
                }
            }
            """,
            moderateThreshold: 4,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ControlFlow_DeeplyNestedLoops()
    {
        // for (+1) > foreach (+2, N=1) > while (+3, N=2) > do (+4, N=3) > if (+5, N=4) + goto (+1) = 16
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M1|}()
                {
                    MyLabel:
                    for (var i = 0; i < 100; i++)
                    {
                        foreach (var c in "")
                        {
                            while (true)
                            {
                                do
                                {
                                    if (true)
                                        goto MyLabel;
                                } while (false);
                            }
                        }
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
    public async Task ControlFlow_SequentialLoops()
    {
        // for (+1) + foreach (+1) + while (+1) + do (+1) + goto (+1) = 5
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M2|}()
                {
                    for (var i = 0; i < 100; i++)
                    {
                    }

                    foreach (var c in "")
                    {
                    }

                    while (true)
                    {
                    }

                    do
                    {
                    } while (false);

                    MyLabel:
                    goto MyLabel;
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ControlFlow_ForEachWithNestedIfs()
    {
        // foreach (+1) > if (+2, N=1) + if (+2, N=1) = 5
        // break and continue without labels do not increment per the spec
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M3|}()
                {
                    foreach (var c in "")
                    {
                        if (true)
                            continue;

                        if (false)
                            break;

                        Console.WriteLine();
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
    public async Task ControlFlow_SimpleSwitch()
    {
        // switch (+1) = 1
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public string {|DL4001:M1|}(int number)
                {
                    switch (number)
                    {
                        case 1: return "one";
                        case 2: return "a couple";
                        case 3: return "a few";
                        default: return "lots";
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
    public async Task ControlFlow_SwitchWithNestedIf()
    {
        // switch (+1) > if (+2, N=1) = 3
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public string {|DL4001:M2|}(int number)
                {
                    switch (number)
                    {
                        case 1:
                            if (true)
                                return "one";
                            return "ONE";
                        case 2:
                            return "a couple";
                        case 3:
                            return "a few";
                        default:
                            return "lots";
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
    public async Task ControlFlow_BreakAndContinueWithoutLabels()
    {
        // for (+1) = 1
        // break and continue without labels are not counted per the spec
        var test = CreateTest(
            """
            public class C
            {
                public void {|DL4001:M|}()
                {
                    for (int i = 0; i < 10; i++)
                    {
                        break;
                        continue;
                    }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }
}
