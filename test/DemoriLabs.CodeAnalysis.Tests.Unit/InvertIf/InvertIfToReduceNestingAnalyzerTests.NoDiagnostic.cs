namespace DemoriLabs.CodeAnalysis.Tests.Unit.InvertIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class InvertIfToReduceNestingAnalyzerTests
{
    [Test]
    public async Task NoDiagnostic_IfWithElse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                    {
                        DoSomething();
                    }
                    else
                    {
                        DoMore();
                    }
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_IfNotLastStatement()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                    {
                        DoSomething();
                    }

                    DoMore();
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_IfWithoutBlock()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                        DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_EmptyBlock()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    if (condition)
                    {
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_FinallyBlock()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void SafeExecute(Action action)
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        if (true)
                        {
                            Console.WriteLine("done");
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_TryBlock()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(bool condition)
                {
                    try
                    {
                        if (condition)
                        {
                            DoSomething();
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_CatchBlock()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M()
                {
                    try
                    {
                        DoSomething();
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null)
                        {
                            Log(ex.InnerException.Message);
                        }
                    }
                }

                private static void DoSomething() { }
                private static void Log(string s) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_NonVoidMethodFollowedByNonExit()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    if (condition)
                    {
                        DoSomething();
                    }

                    DoMore();
                    return 0;
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_ElseIfChainInnerBranchNoExit()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    if (x > 0)
                    {
                        DoPositive();
                    }
                    else if (x < 0)
                    {
                        DoNegative();
                    }
                }

                private static void DoPositive() { }
                private static void DoNegative() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task NoDiagnostic_IfInsideExplicitElseBlock()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a)
                    {
                        DoA();
                    }
                    else
                    {
                        if (b)
                        {
                            DoB();
                        }
                    }
                }

                private static void DoA() { }
                private static void DoB() { }
            }
            """
        );

        await test.RunAsync();
    }
}
