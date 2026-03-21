using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes.RecordDesign;
using DemoriLabs.CodeAnalysis.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class DataClassCouldBeRecordCodeFixTests
{
    private static CSharpCodeFixTest<
        DataClassCouldBeRecordAnalyzer,
        DataClassCouldBeRecordCodeFix,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        var test = new CSharpCodeFixTest<DataClassCouldBeRecordAnalyzer, DataClassCouldBeRecordCodeFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(MutableAttribute).Assembly);
        test.FixedState.AdditionalReferences.Add(typeof(MutableAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task ConvertsSetToInitAndAddsRequired()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
            """,
            """
            public sealed record Person
            {
                public required string Name { get; init; }
                public required int Age { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesInitProperties_AddsRequired()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; init; }
                public int Age { get; init; }
            }
            """,
            """
            public sealed record Person
            {
                public required string Name { get; init; }
                public required int Age { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PropertyWithDefault_NoRequired()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Config|}
            {
                public string Name { get; set; }
                public int Retries { get; set; } = 3;
            }
            """,
            """
            public sealed record Config
            {
                public required string Name { get; init; }
                public int Retries { get; init; } = 3;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesGetOnlyProperties()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; set; }
                public string Upper => Name.ToUpper();
            }
            """,
            """
            public sealed record Person
            {
                public required string Name { get; init; }
                public string Upper => Name.ToUpper();
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RemovesConstructor_ConvertsAssignedPropertiesToRequired()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; set; }

                public Person(string name)
                {
                    Name = name;
                }
            }
            """,
            """
            public sealed record Person
            {
                public required string Name { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RemovesConstructor_GetOnlyPropertiesToRequiredInit()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Event|}
            {
                public int OrderId { get; }
                public string Description { get; }

                public Event(int orderId, string description)
                {
                    OrderId = orderId;
                    Description = description;
                }
            }
            """,
            """
            public sealed record Event
            {
                public required int OrderId { get; init; }
                public required string Description { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorWithDefaults_NonRequiredWithInitializer()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Config|}
            {
                public int Retries { get; }
                public string Env { get; }

                public Config(int retries = 3, string env = "prod")
                {
                    Retries = retries;
                    Env = env;
                }
            }
            """,
            """
            public sealed record Config
            {
                public int Retries { get; init; } = 3;
                public string Env { get; init; } = "prod";
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorCallSite_RewrittenToObjectInitializer()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Event|}
            {
                public int Id { get; }
                public string Name { get; }

                public Event(int id, string name)
                {
                    Id = id;
                    Name = name;
                }
            }

            public class Consumer
            {
                public void M()
                {
                    var e = new Event(1, "test");
                }
            }
            """,
            """
            public sealed record Event
            {
                public required int Id { get; init; }
                public required string Name { get; init; }
            }

            public class Consumer
            {
                public void M()
                {
                    var e = new Event
                    {
                        Id = 1,
                        Name = "test"
                    };
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RemovesRecordSynthesisableMethodsAndAddsSealed()
    {
        var test = CreateTest(
            """
            using System;

            public class {|DL1004:Person|} : IEquatable<Person>
            {
                public string Name { get; set; }
                public int Age { get; set; }

                public override bool Equals(object? obj) => Equals(obj as Person);

                public bool Equals(Person? other)
                {
                    return other is not null && Name == other.Name;
                }

                public override int GetHashCode() => HashCode.Combine(Name, Age);
                public static bool operator ==(Person? left, Person? right) => Equals(left, right);
                public static bool operator !=(Person? left, Person? right) => !Equals(left, right);
                public override string ToString() => $"Person {{ Name = {Name} }}";

                public void Deconstruct(out string name, out int age)
                {
                    name = Name;
                    age = Age;
                }
            }
            """,
            """
            using System;

            public sealed record Person : IEquatable<Person>
            {
                public required string Name { get; init; }
                public required int Age { get; init; }

                public bool Equals(Person? other)
                {
                    return other is not null && Name == other.Name;
                }

                public override int GetHashCode() => HashCode.Combine(Name, Age);
                public override string ToString() => $"Person {{ Name = {Name} }}";

                public void Deconstruct(out string name, out int age)
                {
                    name = Name;
                    age = Age;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExpressionBodiedConstructor_RemovesAndConvertsProperty()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Event|}
            {
                public int OrderId { get; }

                public Event(int orderId)
                    => OrderId = orderId;
            }
            """,
            """
            public sealed record Event
            {
                public required int OrderId { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PreservesStaticMethods()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; set; }

                public Person(string name) { Name = name; }

                public static Person Default() => new Person("Unknown");
            }
            """,
            """
            public sealed record Person
            {
                public required string Name { get; init; }

                public static Person Default() => new Person
                {
                    Name = "Unknown"
                };
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PrimaryConstructorCallSite_RewrittenToObjectInitializer()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class {|DL1004:Event|}(
                [ReadOnly] int orderId
            )
            {
                public int OrderId { get; } = orderId;
            }

            public class Consumer
            {
                public void M()
                {
                    var e = new Event(42);
                }
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public sealed record Event
            {
                public required int OrderId { get; init; }
            }

            public class Consumer
            {
                public void M()
                {
                    var e = new Event
                    {
                        OrderId = 42
                    };
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithPrimaryConstructor_RemovesParametersAndInitializers()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public class {|DL1004:Event|}(
                [ReadOnly] int orderId,
                [ReadOnly] string name
            )
            {
                public int OrderId { get; } = orderId;
                public string Name { get; } = name;
            }
            """,
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            public sealed record Event
            {
                public required int OrderId { get; init; }
                public required string Name { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GenericClass_ConvertsToRecord()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Wrapper|}<T>
            {
                public T Value { get; set; }
            }
            """,
            """
            public sealed record Wrapper<T>
            {
                public required T Value { get; init; }
            }
            """
        );

        await test.RunAsync();
    }
}
