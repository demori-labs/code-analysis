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
    public async Task EmptyBodyForwardingAllParametersToBase()
    {
        var test = CreateTest(
            """
            public abstract class HandlerBase<T>
            {
                public HandlerBase(string name, T value) { }
            }

            public class ConcreteHandler : HandlerBase<int>
            {
                public {|DL1005:ConcreteHandler|}(string name, int value) : base(name, value)
                {
                }

                public void Execute() { }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public abstract class HandlerBase<T>
            {
                public HandlerBase(string name, T value) { }
            }

            public class ConcreteHandler(
                [ReadOnly] string name,
                [ReadOnly] int value
            ) : HandlerBase<int>(name, value)
            {
                public void Execute() { }
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

    [Test]
    public async Task RenamesPrivateFieldReferencesAcrossSameFile()
    {
        var test = CreateTest(
            """
            public class Base
            {
                public Base(object logger) { }
            }

            public class OrderService : Base
            {
                private readonly object _Repository;

                public {|DL1005:OrderService|}(object logger, object repository) : base(logger)
                {
                    _Repository = repository;
                }

                public object GetRepo() => _Repository;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class Base
            {
                public Base(object logger) { }
            }

            public class OrderService(
                [ReadOnly] object logger,
                [ReadOnly] object repository
            ) : Base(logger)
            {
                public object GetRepo() => repository;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ProtectedField_KeptAsInitialisedField()
    {
        var test = CreateTest(
            """
            public class ServiceBase
            {
                protected readonly object Mediator;

                public {|DL1005:ServiceBase|}(object mediator)
                {
                    Mediator = mediator;
                }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class ServiceBase(
                [ReadOnly] object mediator
            )
            {
                protected readonly object Mediator = mediator;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConvertsPropertyAssignmentsToInitializedProperties()
    {
        var test = CreateTest(
            """
            public class OrderHandler
            {
                public int OrderId { get; }
                public string Name { get; }

                public {|DL1005:OrderHandler|}(int orderId, string name)
                {
                    OrderId = orderId;
                    Name = name;
                }

                public string Display() => $"{OrderId}: {Name}";
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class OrderHandler(
                [ReadOnly] int orderId,
                [ReadOnly] string name
            )
            {
                public int OrderId { get; } = orderId;
                public string Name { get; } = name;

                public string Display() => $"{OrderId}: {Name}";
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConvertsMixOfFieldsAndPropertiesToPrimaryConstructor()
    {
        var test = CreateTest(
            """
            public class OrderHandler
            {
                private readonly object _logger;
                public int OrderId { get; }

                public {|DL1005:OrderHandler|}(object logger, int orderId)
                {
                    _logger = logger;
                    OrderId = orderId;
                }

                public object GetLogger() => _logger;
                public int GetId() => OrderId;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class OrderHandler(
                [ReadOnly] object logger,
                [ReadOnly] int orderId
            )
            {
                public int OrderId { get; } = orderId;

                public object GetLogger() => logger;
                public int GetId() => OrderId;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesExistingParameterAttributes()
    {
        var test = CreateTest(
            """
            using System.ComponentModel.DataAnnotations;

            public class MyService
            {
                private readonly string _name;

                public {|DL1005:MyService|}([Required] string name)
                {
                    _name = name;
                }

                public string GetName() => _name;
            }
            """,
            """
            using System.ComponentModel.DataAnnotations;
            using DemoriLabs.CodeAnalysis.Attributes;

            public class MyService(
                [Required][ReadOnly] string name
            )
            {
                public string GetName() => name;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MixedPrivateAndProtectedFields_PrivateRemovedProtectedKept()
    {
        var test = CreateTest(
            """
            public class ServiceBase
            {
                protected readonly object Mediator;
                private readonly object _logger;

                public {|DL1005:ServiceBase|}(object mediator, object logger)
                {
                    Mediator = mediator;
                    _logger = logger;
                }

                public object GetLogger() => _logger;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class ServiceBase(
                [ReadOnly] object mediator,
                [ReadOnly] object logger
            )
            {
                protected readonly object Mediator = mediator;

                public object GetLogger() => logger;
            }
            """
        );

        await test.RunAsync();
    }
}
