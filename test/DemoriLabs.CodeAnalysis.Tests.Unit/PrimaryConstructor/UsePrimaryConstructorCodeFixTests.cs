using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.PrimaryConstructor;
using DemoriLabs.CodeAnalysis.PrimaryConstructor;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PrimaryConstructor;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UsePrimaryConstructorCodeFixTests
{
    private static CSharpCodeFixTest<
        UsePrimaryConstructorAnalyzer,
        UsePrimaryConstructorCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        var test = new CSharpCodeFixTest<UsePrimaryConstructorAnalyzer, UsePrimaryConstructorCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(ReadOnlyAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task ConvertsSimpleConstructorToPrimaryConstructor()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly int _id;
                private readonly string _name;

                public {|DL1005:MyService|}(int id, string name)
                {
                    _id = id;
                    _name = name;
                }

                public int GetId() => _id;
                public string GetName() => _name;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class MyService(
                [ReadOnly] int id,
                [ReadOnly] string name
            )
            {
                public int GetId() => id;
                public string GetName() => name;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StripsFieldPrefixesAndUsesCamelCase()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly string _httpClient;
                private readonly int m_count;

                public {|DL1005:MyService|}(string httpClient, int count)
                {
                    _httpClient = httpClient;
                    m_count = count;
                }

                public string GetClient() => _httpClient;
                public int GetCount() => m_count;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class MyService(
                [ReadOnly] string httpClient,
                [ReadOnly] int count
            )
            {
                public string GetClient() => httpClient;
                public int GetCount() => count;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesBaseInitializer()
    {
        var test = CreateTest(
            """
            public class Base
            {
                public Base(int id) { }
            }

            public class Derived : Base
            {
                private readonly string _name;

                public {|DL1005:Derived|}(int id, string name) : base(id)
                {
                    _name = name;
                }

                public string GetName() => _name;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Base
            {
                public Base(int id) { }
            }

            public class Derived(
                [ReadOnly] int id,
                [ReadOnly] string name
            ) : Base(id)
            {
                public string GetName() => name;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReplacesFieldReferencesInMethods()
    {
        var test = CreateTest(
            """
            public class Calculator
            {
                private readonly int _value;

                public {|DL1005:Calculator|}(int value)
                {
                    _value = value;
                }

                public int Double() => _value * 2;
                public bool IsPositive() => _value > 0;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Calculator(
                [ReadOnly] int value
            )
            {
                public int Double() => value * 2;
                public bool IsPositive() => value > 0;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConvertsStructToPrimaryConstructor()
    {
        var test = CreateTest(
            """
            public struct Point
            {
                private readonly int _x;
                private readonly int _y;

                public {|DL1005:Point|}(int x, int y)
                {
                    _x = x;
                    _y = y;
                }

                public int Sum() => _x + _y;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public struct Point(
                [ReadOnly] int x,
                [ReadOnly] int y
            )
            {
                public int Sum() => x + y;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReplacesThisFieldReferences()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly int _id;

                public {|DL1005:MyService|}(int id)
                {
                    this._id = id;
                }

                public int GetId() => this._id;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class MyService(
                [ReadOnly] int id
            )
            {
                public int GetId() => id;
            }
            """
        );

        await test.RunAsync();
    }
}
