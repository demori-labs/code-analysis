using DemoriLabs.Diagnostics.Attributes;

namespace DemoriLabs.Diagnostics.Tests.Unit.CognitiveComplexity;

public partial class CognitiveComplexityAnalyzerTests
{
    [Test]
    public async Task Suppression_SuppressOnMethod_NoDiagnostic()
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
    public async Task Suppression_SuppressOnClass_NoDiagnostic()
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
    public async Task Suppression_ThresholdOverrideModerate_NoDiagnostic()
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
    public async Task Suppression_ThresholdStillExceedsModerate()
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
    public async Task Suppression_ThresholdOnClass_NoDiagnostic()
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
    public async Task Suppression_ThresholdMethodOverridesClass()
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
    public async Task Suppression_ThresholdOverrideBoth()
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
}
