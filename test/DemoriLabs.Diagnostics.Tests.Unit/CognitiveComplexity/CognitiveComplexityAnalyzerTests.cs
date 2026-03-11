using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.Attributes;
using DemoriLabs.Diagnostics.CognitiveComplexity;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.CognitiveComplexity;

// ReSharper disable MemberCanBeMadeStatic.Global
public class CognitiveComplexityAnalyzerTests
{
    private static CSharpAnalyzerTest<CognitiveComplexityAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        int? moderateThreshold = null,
        int? elevatedThreshold = null
    )
    {
        var test = new CSharpAnalyzerTest<CognitiveComplexityAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        if (moderateThreshold is null && elevatedThreshold is null)
            return test;

        var moderate = moderateThreshold ?? CognitiveComplexityAnalyzer.DefaultModerateThreshold;
        var elevated = elevatedThreshold ?? CognitiveComplexityAnalyzer.DefaultElevatedThreshold;

        test.TestState.AnalyzerConfigFiles.Add(
            (
                "/.config",
                $"""
                is_global = true
                dotnet_diagnostic.DL4001.cognitive_complexity_moderate_threshold = {moderate}
                dotnet_diagnostic.DL4001.cognitive_complexity_elevated_threshold = {elevated}
                """
            )
        );

        return test;
    }

    [Test]
    public async Task Condition_IfElse_Complexity2()
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
    public async Task Condition_NestedIfElseIfElse_Complexity5()
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
    public async Task Looping_DeeplyNestedLoops_Complexity16()
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
    public async Task Looping_SequentialLoops_Complexity5()
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
    public async Task Looping_ForEachWithNestedIfs_Complexity5()
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
    public async Task LogicalOperator_Sequences_Complexity3()
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
    public async Task LogicalOperator_MixedInCondition_Complexity4()
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
    public async Task NullChecking_TraditionalIfCheck_Complexity1()
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
    public async Task NullChecking_NullConditionalOperator_Complexity0()
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

    [Test]
    public async Task Switch_SimpleSwitch_Complexity1()
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
    public async Task Switch_WithNestedIf_Complexity3()
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
    public async Task TryCatch_NestedControlFlowAndCatch_Complexity9()
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
    public async Task TryCatch_CatchInsideIf_Complexity3()
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
    public async Task TryCatch_CatchWithWhenFilter_Complexity4()
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
    public async Task TryCatch_CatchWithNestedIf_Complexity6()
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

    [Test]
    public async Task Lambda_IfInsideLambda_Complexity2()
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
    public async Task Lambda_IfInsideAnonymousDelegate_Complexity2()
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
    public async Task Recursion_DirectRecursion_Complexity2()
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
    public async Task Recursion_MultipleCallSites_StillOnlyOneIncrement()
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
    public async Task Recursion_NoRecursion_NoIncrement()
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

    [Test]
    public async Task Elevated_ExceedsElevatedThreshold_ReportsDL4002()
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
    public async Task Elevated_BetweenThresholds_ReportsModerate()
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
    public async Task Elevated_ExceedsElevated_DoesNotReportModerate()
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
    public async Task MethodBelowThreshold_NoDiagnostic()
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
    public async Task MethodAtExactThreshold_NoDiagnostic()
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

    [Test]
    public async Task SuppressCognitiveComplexity_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                [SuppressCognitiveComplexity]
                public void M(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 0
        );

        test.TestState.AdditionalReferences.Add(typeof(SuppressCognitiveComplexityAttribute).Assembly);

        await test.RunAsync();
    }

    [Test]
    public async Task SuppressCognitiveComplexity_OnClass_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            [SuppressCognitiveComplexity]
            public class C
            {
                public void M(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 0
        );

        test.TestState.AdditionalReferences.Add(typeof(SuppressCognitiveComplexityAttribute).Assembly);

        await test.RunAsync();
    }

    [Test]
    public async Task CognitiveComplexityThreshold_OverrideModerate_NoDiagnostic()
    {
        // Complexity is 10 (4 nested ifs), attribute sets moderate to 10, elevated defaults to 15
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                [CognitiveComplexityThreshold(moderateThreshold: 10)]
                public void M(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        test.TestState.AdditionalReferences.Add(typeof(CognitiveComplexityThresholdAttribute).Assembly);

        await test.RunAsync();
    }

    [Test]
    public async Task CognitiveComplexityThreshold_StillExceedsModerate_ReportsDiagnostic()
    {
        // Complexity is 10, attribute sets moderate to 5, elevated defaults to 15
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                [CognitiveComplexityThreshold(moderateThreshold: 5)]
                public void {|DL4001:M|}(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 100,
            elevatedThreshold: 1000
        );

        test.TestState.AdditionalReferences.Add(typeof(CognitiveComplexityThresholdAttribute).Assembly);

        await test.RunAsync();
    }

    [Test]
    public async Task CognitiveComplexityThreshold_OnClass_NoDiagnostic()
    {
        // Complexity is 10, class attribute sets moderate to 10, elevated defaults to 15
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            [CognitiveComplexityThreshold(moderateThreshold: 10)]
            public class C
            {
                public void M(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 0,
            elevatedThreshold: 1000
        );

        test.TestState.AdditionalReferences.Add(typeof(CognitiveComplexityThresholdAttribute).Assembly);

        await test.RunAsync();
    }

    [Test]
    public async Task CognitiveComplexityThreshold_MethodOverridesClass()
    {
        // Class sets moderate to 100, method overrides to 5. Complexity is 10.
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            [CognitiveComplexityThreshold(moderateThreshold: 100)]
            public class C
            {
                [CognitiveComplexityThreshold(moderateThreshold: 5)]
                public void {|DL4001:M|}(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 100,
            elevatedThreshold: 1000
        );

        test.TestState.AdditionalReferences.Add(typeof(CognitiveComplexityThresholdAttribute).Assembly);

        await test.RunAsync();
    }

    [Test]
    public async Task CognitiveComplexityThreshold_OverrideBothThresholds()
    {
        // Complexity is 10, attribute sets moderate to 5, elevated to 8
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                [CognitiveComplexityThreshold(moderateThreshold: 5, elevatedThreshold: 8)]
                public void {|DL4002:M|}(bool a, bool b, bool c, bool d)
                {
                    if (a) { if (b) { if (c) { if (d) { } } } }
                }
            }
            """,
            moderateThreshold: 100,
            elevatedThreshold: 1000
        );

        test.TestState.AdditionalReferences.Add(typeof(CognitiveComplexityThresholdAttribute).Assembly);

        await test.RunAsync();
    }

    [Test]
    public async Task BreakAndContinue_WithoutLabels_NoIncrement()
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
