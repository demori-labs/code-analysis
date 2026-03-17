namespace DemoriLabs.Diagnostics.Tests.Unit.CognitiveComplexity;

public partial class CognitiveComplexityAnalyzerTests
{
    [Test]
    public async Task Nesting_IfInsideLambda()
    {
        // lambda (nesting) > if (+2, N=1) = 2
        var test = CreateTest(
            """
            using System;
            using System.Threading.Tasks;
            public class A
            {
                public void {|DL4001:M1|}(bool b)
                {
                    var task = Task.Run(() =>
                    {
                        if (b)
                            Console.WriteLine();
                    });
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Nesting_IfInsideAnonymousDelegate()
    {
        // delegate (nesting) > if (+2, N=1) = 2
        var test = CreateTest(
            """
            using System;
            using System.Threading.Tasks;
            public class A
            {
                public void {|DL4001:M2|}(bool b)
                {
                    var task = Task.Run(delegate
                    {
                        if (b)
                            Console.WriteLine();
                    });
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Nesting_DirectRecursion()
    {
        // if (+1) + recursion (+1) = 2
        var test = CreateTest(
            """
            public class A
            {
                public int {|DL4001:Factorial|}(int n)
                {
                    if (n <= 1)
                        return 1;
                    return n * Factorial(n - 1);
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Nesting_MultipleRecursiveCallsSingleIncrement()
    {
        // if (+1) + recursion (+1) = 2
        // Multiple recursive calls still only add +1 total
        var test = CreateTest(
            """
            public class A
            {
                public int {|DL4001:Fib|}(int n)
                {
                    if (n <= 1)
                        return n;
                    return Fib(n - 1) + Fib(n - 2);
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Nesting_NoRecursionNoIncrement()
    {
        // if (+1) = 1
        var test = CreateTest(
            """
            public class A
            {
                public int {|DL4001:M|}(int n)
                {
                    if (n > 0)
                        return n;
                    return 0;
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }
}
