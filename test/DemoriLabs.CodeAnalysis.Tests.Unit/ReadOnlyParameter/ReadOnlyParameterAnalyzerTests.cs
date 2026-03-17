using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.ReadOnlyParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.ReadOnlyParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ReadOnlyParameterAnalyzerTests
{
    private static CSharpAnalyzerTest<ReadOnlyParameterAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        var test = new CSharpAnalyzerTest<ReadOnlyParameterAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task SimpleAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x = 5|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AddAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x += 1|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SubtractAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x -= 1|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CoalesceAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] string? x)
                {
                    {|DL2001:x ??= "default"|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PostfixIncrement_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x++|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PostfixDecrement_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x--|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrefixIncrement_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:++x|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrefixDecrement_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:--x|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadingParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public int M([ReadOnly] int x) => x * 2;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MethodCallOnParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public string M([ReadOnly] object x) => x.ToString();
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoAttribute_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    x = 5;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadOnlyOnOutParameter_ReportsDL2002()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([{|DL2002:ReadOnly|}] out int x)
                {
                    x = 5;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadOnlyOnRefParameter_ReportsDL2002()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([{|DL2002:ReadOnly|}] ref int x)
                {
                    x = 5;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadOnlyOnInParameter_ReportsDL2002()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([{|DL2002:ReadOnly|}] in int x)
                {
                    _ = x;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadOnlyOnNormalParameter_NoDL2002()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    _ = x;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Widget
            {
                public Widget([ReadOnly] int count)
                {
                    {|DL2001:count = 0|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LocalFunctionParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M()
                {
                    static void Inner([ReadOnly] int x)
                    {
                        {|DL2001:x = 10|};
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrimaryConstructorParameter_ReassignedInMethod_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Widget([ReadOnly] int count)
            {
                public void Reset()
                {
                    {|DL2001:count = 0|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrimaryConstructorParameter_CompoundAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Counter([ReadOnly] int value)
            {
                public void Add(int amount)
                {
                    {|DL2001:value += amount|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrimaryConstructorParameter_Increment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Counter([ReadOnly] int value)
            {
                public void Tick()
                {
                    {|DL2001:value++|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrimaryConstructorParameter_UnannotatedSibling_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Point([ReadOnly] int x, int y)
            {
                public void ShiftY(int delta)
                {
                    y += delta;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordClassPrimaryConstructorParameter_ReportsIncompatible()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public record Widget([{|DL2002:ReadOnly|}] int Count);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReadonlyRecordStructPrimaryConstructorParameter_ReportsIncompatible()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public readonly record struct Point([{|DL2002:ReadOnly|}] int X);
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordStructPrimaryConstructorParameter_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public record struct Counter([ReadOnly] int Value)
            {
                public void Reset()
                {
                    {|DL2001:Value = 0|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrimaryConstructorParameter_ReadOnly_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Widget([ReadOnly] int count)
            {
                public int GetCount() => count;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParameterShadowsMember_ThisMemberAssignment_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Widget
            {
                private int count;

                public Widget([ReadOnly] int count)
                {
                    this.count = count;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ParameterShadowsMember_DirectParameterAssignment_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Widget
            {
                private int count;

                public Widget([ReadOnly] int count)
                {
                    this.count = count;
                    {|DL2001:count = 0|};
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrimaryConstructorParam_ShadowedProperty_ThisAccessAllowed()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Widget([ReadOnly] int count)
            {
                public int Count { get; set; } = count;

                public void Reset()
                {
                    Count = 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task OnlyAnnotatedParameterFlagged()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class C
            {
                public void M([ReadOnly] int x, int y)
                {
                    {|DL2001:x = 1|};
                    y = 2;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
