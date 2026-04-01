using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.EmptyString;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.EmptyString;

// ReSharper disable MemberCanBeMadeStatic.Global
public class UseIsNullOrEmptyAnalyzerTests
{
    private static CSharpAnalyzerTest<UseIsNullOrEmptyAnalyzer, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<UseIsNullOrEmptyAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task EqualsEmptyString_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s == ""|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsEmptyString_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s != ""|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReversedEmptyString_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:"" == s|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task EqualsStringEmpty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s == string.Empty|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NotEqualsStringEmpty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s != string.Empty|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LengthEqualsZero_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Length == 0|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LengthNotEqualsZero_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Length != 0|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LengthIsZero_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Length is 0|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LengthIsNotZero_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Length is not 0|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullConditionalLengthEqualsZero_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if ({|DL5002:s?.Length == 0|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ReversedLengthEqualsZero_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:0 == s.Length|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsEmptyString_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s is ""|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotEmptyString_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s is not ""|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNullOrEmpty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if ({|DL5002:s is null or ""|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IsNotNullAndNotEmpty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if ({|DL5002:s is not null and not ""|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StaticStringEquals_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:string.Equals(s, "")|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task StaticStringEqualsWithComparison_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:string.Equals(s, "", StringComparison.Ordinal)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InstanceEquals_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Equals("")|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InstanceEqualsStringEmpty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if ({|DL5002:s.Equals(string.Empty)|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task MultipleDiagnosticsInSameMethod()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string a, string b)
                {
                    if ({|DL5002:a == ""|}) { }
                    if ({|DL5002:b.Length == 0|}) { }
                    if ({|DL5002:a is ""|}) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InProperty_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                private string _s = "";
                public bool IsEmpty => {|DL5002:_s == ""|};
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NonEmptyString_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s == "hello") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NullCheck_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if (s == null) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task LengthNotZero_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string s)
                {
                    if (s.Length == 5) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ArrayLength_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int[] arr)
                {
                    if (arr.Length == 0) { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideExpressionTree_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq;
            using System.Linq.Expressions;

            public class C
            {
                public void M(IQueryable<string> q)
                {
                    q.Where(s => s == "");
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task InsideExpressionTreeExplicit_NoDiagnostic()
    {
        var test = CreateTest(
            """
            using System;
            using System.Linq.Expressions;

            public class C
            {
                public void M()
                {
                    Expression<Func<string, bool>> expr = s => s == "";
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ComplexOrPattern_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if (s is null or "" or "N/A") { }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task AlreadyUsingIsNullOrEmpty_NoDiagnostic()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(string? s)
                {
                    if (string.IsNullOrEmpty(s)) { }
                }
            }
            """
        );

        await test.RunAsync();
    }
}
