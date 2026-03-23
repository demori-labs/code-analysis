namespace DemoriLabs.CodeAnalysis.Tests.Unit.CognitiveComplexity;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class CognitiveComplexityAnalyzerTests
{
    [Test]
    public async Task ExceptionHandling_NestedControlFlowAndCatch()
    {
        // if (+1) > for (+2, N=1) > while (+3, N=2) + catch (+1) > if (+2, N=1) = 9
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M1|}(bool a, bool b)
                {
                    try
                    {
                        if (a)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                while (b)
                                {
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (b)
                        {
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
    public async Task ExceptionHandling_CatchInsideIf()
    {
        // if (+1) > catch (+2, N=1) = 3
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M2|}()
                {
                    if (true)
                    {
                        try { throw new Exception("ErrorType1"); }
                        catch (IndexOutOfRangeException ex)
                        {
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
    public async Task ExceptionHandling_CatchWithWhenFilter()
    {
        // if (+1) > catch (+2, N=1) + when (+1) = 4
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M3|}()
                {
                    if (true)
                    {
                        try { throw new Exception("ErrorType1"); }
                        catch (Exception ex) when (ex.Message == "ErrorType2")
                        {
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
    public async Task ExceptionHandling_CatchWithNestedIf()
    {
        // if (+1) > catch (+2, N=1) > if (+3, N=2) = 6
        var test = CreateTest(
            """
            using System;
            public class A
            {
                public void {|DL4001:M4|}()
                {
                    if (true)
                    {
                        try { throw new Exception("ErrorType1"); }
                        catch (Exception ex)
                        {
                            if (ex.Message == "ErrorType3")
                            {
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
}
