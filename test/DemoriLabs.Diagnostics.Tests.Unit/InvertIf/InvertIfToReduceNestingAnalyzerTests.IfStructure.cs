namespace DemoriLabs.Diagnostics.Tests.Unit.InvertIf;

public partial class InvertIfToReduceNestingAnalyzerTests
{
    [Test]
    public async Task IfStructure_MultipleStatementsInBody()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
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
    public async Task IfStructure_StatementsBeforeIf()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    var x = 1;
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStructure_NestedIfOnlyOuterFlagged()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    {|DL3002:if|} (a)
                    {
                        if (b)
                        {
                            DoSomething();
                        }
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStructure_NonVoidMultipleStatementsFollowedByReturn()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                        return 42;
                    }

                    return 0;
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStructure_NonVoidSingleStatementFollowedByReturn()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        return 42;
                    }

                    return 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task IfStructure_NonVoidBodyWithoutReturnFollowedByReturn()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                        DoMore();
                    }

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
    public async Task IfStructure_IfElseWhereElseEndsWithReturn()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                        DoMore();
                    }
                    else
                    {
                        return;
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
    public async Task IfStructure_ElseIfChainAllBranchesEndWithExit()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(int x)
                {
                    {|DL3002:if|} (x > 0)
                    {
                        DoPositive();
                    }
                    else if (x < 0)
                    {
                        return;
                    }
                }

                private static void DoPositive() { }
            }
            """
        );

        await test.RunAsync();
    }
}
