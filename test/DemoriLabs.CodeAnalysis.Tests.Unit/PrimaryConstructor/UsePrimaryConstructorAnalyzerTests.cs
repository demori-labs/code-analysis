using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.Attributes;
using DemoriLabs.CodeAnalysis.PrimaryConstructor;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.PrimaryConstructor;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UsePrimaryConstructorAnalyzerTests
{
    private static CSharpAnalyzerTest<UsePrimaryConstructorAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        var test = new CSharpAnalyzerTest<UsePrimaryConstructorAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.TestState.AdditionalReferences.Add(typeof(MutableAttribute).Assembly);
        return test;
    }

    [Test]
    public async Task ClassWithSimpleConstructorAssigningReadonlyFields_ReportsDiagnostic()
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
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StructWithSimpleConstructor_ReportsDiagnostic()
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
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorWithBaseInitializer_ReportsDiagnostic()
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
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorWithThisFieldAccess_ReportsDiagnostic()
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
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithMultipleConstructors_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly int _id;

                public MyService(int id)
                {
                    _id = id;
                }

                public MyService() : this(0)
                {
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassWithNoExplicitConstructor_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly int _id = 42;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ClassAlreadyHasPrimaryConstructor_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class MyService(int id)
            {
                private readonly int _id = id;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorWithLogic_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly int _id;

                public MyService(int id)
                {
                    if (id < 0) 
                        throw new System.ArgumentException("id");
                    
                    _id = id;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorAssignsNonReadonlyField_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private int _id;

                public MyService(int id)
                {
                    _id = id;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AbstractClass_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public abstract class MyService
            {
                private readonly int _id;

                public {|DL1005:MyService|}(int id)
                {
                    _id = id;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task RecordType_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public record MyRecord
            {
                private readonly int _id;

                public MyRecord(int id)
                {
                    _id = id;
                }
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
                public static int Value { get; set; }
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
            public partial class MyService
            {
                private readonly int _id;

                public MyService(int id)
                {
                    _id = id;
                }
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
            public class MyService
            {
                private readonly int _id;

                public MyService(int id)
                {
                    _id = id;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorWithUnmappedParameter_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly int _id;

                public MyService(int id, string name)
                {
                    _id = id;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConstructorWithMethodCall_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class MyService
            {
                private readonly int _id;

                public MyService(int id)
                {
                    _id = id;
                    Initialize();
                }

                private void Initialize() { }
            }
            """
        );

        await test.RunAsync();
    }
}
