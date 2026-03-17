using System.Diagnostics.CodeAnalysis;
using DemoriLabs.Diagnostics.Attributes;
using DemoriLabs.Diagnostics.CodeFixes;
using DemoriLabs.Diagnostics.ReadOnlyParameter;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.Diagnostics.Tests.Unit.ReadOnlyParameter;

// ReSharper disable MemberCanBeMadeStatic.Global
public class ReadOnlyParameterCodeFixTests
{
    private static CSharpCodeFixTest<ReadOnlyParameterAnalyzer, ReadOnlyParameterCodeFix, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source,
        [StringSyntax("C#")] string fixedSource
    )
    {
        var test = new CSharpCodeFixTest<ReadOnlyParameterAnalyzer, ReadOnlyParameterCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task SimpleAssignment_IntroducesLocalVariable()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x = 10|};
                }
            }
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    var xLocal = 10;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task CompoundAssignment_IntroducesLocalWithExpression()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x += 5|};
                }
            }
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    var xLocal = x + 5;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PostIncrement_IntroducesLocalWithAddition()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x++|};
                }
            }
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    var xLocal = x + 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreDecrement_IntroducesLocalWithSubtraction()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:--x|};
                }
            }
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    var xLocal = x - 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task SubsequentReferences_AreReplacedWithLocal()
    {
        var test = CreateTest(
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    {|DL2001:x = 10|};
                    System.Console.WriteLine(x);
                }
            }
            """,
            """
            using DemoriLabs.Diagnostics.Attributes;

            public class C
            {
                public void M([ReadOnly] int x)
                {
                    var xLocal = 10;
                    System.Console.WriteLine(xLocal);
                }
            }
            """
        );

        await test.RunAsync();
    }
}
