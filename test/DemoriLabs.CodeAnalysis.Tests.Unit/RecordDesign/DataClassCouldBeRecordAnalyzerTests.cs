using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.RecordDesign;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.RecordDesign;

// ReSharper disable MemberCanBeMadeStatic.Global
public class DataClassCouldBeRecordAnalyzerTests
{
    private static CSharpAnalyzerTest<DataClassCouldBeRecordAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        var test = new CSharpAnalyzerTest<DataClassCouldBeRecordAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(MutableAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task ClassWithOnlyProperties_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithInitProperties_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public string Name { get; init; }
                public int Age { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithConstructorAndProperties_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Person|}
            {
                public Person(string name, int age)
                {
                    Name = name;
                    Age = age;
                }

                public string Name { get; set; }
                public int Age { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithMethod_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Person
            {
                public string Name { get; set; }
                public int Age { get; set; }

                public string GetDisplayName() => Name + " (" + Age + ")";
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithField_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Person
            {
                private readonly int _id;
                public string Name { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithEvent_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class Person
            {
                public string Name { get; set; }
                public event EventHandler? NameChanged;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithNoProperties_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class Empty
            {
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordClass_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record Person
            {
                public string Name { get; init; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AbstractClass_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public abstract class Person
            {
                public string Name { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StaticClass_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public static class Config
            {
                public static string Name { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithBaseClass_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class {|DL1004:BaseEntity|}
            {
                public int Id { get; set; }
            }

            public class Person : BaseEntity
            {
                public string Name { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithMutableAttribute_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using DemoriLabs.CodeAnalysis.Attributes;

            [Mutable]
            public class Person
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassImplementingInterface_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public interface IEntity
            {
                int Id { get; }
            }

            public class {|DL1004:Person|} : IEntity
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task PartialClass_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public partial class Person
            {
                public string Name { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Struct_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public struct Point
            {
                public int X { get; set; }
                public int Y { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task GenericClass_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class {|DL1004:Wrapper|}<T>
            {
                public T Value { get; set; }
            }
            """
        );

        await test.RunAsync();
    }
}
