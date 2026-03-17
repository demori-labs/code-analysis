namespace DemoriLabs.CodeAnalysis.Tests.Unit.CognitiveComplexity;

public partial class CognitiveComplexityAnalyzerTests
{
    [Test]
    public async Task Threshold_ExceedsElevated_ReportsDL4002()
    {
        // if (+1) > if (+2) > if (+3) > if (+4) = 10
        var test = CreateTest(
            """
            public class C
            {
                public void {|DL4002:M|}(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 5
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_BetweenThresholds_ReportsModerate()
    {
        // if (+1) > if (+2) > if (+3) > if (+4) = 10
        var test = CreateTest(
            """
            public class C
            {
                public void {|DL4001:M|}(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 5,
            elevatedThreshold: 15
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_ExceedsElevated_DoesNotReportModerate()
    {
        // if (+1) > if (+2) > if (+3) > if (+4) = 10, only DL4002 should fire
        var test = CreateTest(
            """
            public class C
            {
                public void {|DL4002:M|}(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 5
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_BelowThreshold_NoDiagnostic()
    {
        // if (+1) else if (+1) = 2
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x > 0) { }
                    else if (x < 0) { }
                }
            }
            """,
            moderateThreshold: 5,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Threshold_AtExactThreshold_NoDiagnostic()
    {
        // if (+1) = 1, threshold is 1 — equal, no diagnostic (only exceeding triggers)
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a)
                {
                    if (a) { }
                }
            }
            """,
            moderateThreshold: 1,
            elevatedThreshold: 1000
        );

        await test.RunAsync();
    }
}
