namespace DemoriLabs.CodeAnalysis.Tests.Unit.InvertIf;

public partial class InvertIfToReduceNestingCodeFixTests
{
    [Test]
    public async Task ConditionNegation_NotEqualsNullToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void Process(Order order)
                {
                    {|DL3002:if|} (order != null)
                    {
                        order.Validate();
                        order.Submit();
                    }
                }

                private static void DoSomething() { }
            }

            public class Order
            {
                public void Validate() { }
                public void Submit() { }
            }
            """,
            """
            public class C
            {
                public void Process(Order order)
                {
                    if (order is null)
                        return;

                    order.Validate();
                    order.Submit();
                }

                private static void DoSomething() { }
            }

            public class Order
            {
                public void Validate() { }
                public void Submit() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_LogicalNotRemoved()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void SendEmail(string address)
                {
                    {|DL3002:if|} (!string.IsNullOrEmpty(address))
                    {
                        var client = new object();
                        Send(address, "Hello");
                    }
                }

                private static void Send(string a, string b) { }
            }
            """,
            """
            public class C
            {
                public void SendEmail(string address)
                {
                    if (string.IsNullOrEmpty(address))
                        return;

                    var client = new object();
                    Send(address, "Hello");
                }

                private static void Send(string a, string b) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_CompoundAndToOr()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void Execute(MyTask task)
                {
                    {|DL3002:if|} (task != null && task.IsEnabled && !task.IsCompleted)
                    {
                        task.Run();
                        task.MarkCompleted();
                        Audit(task.Id);
                    }
                }

                private static void Audit(int id) { }
            }

            public class MyTask
            {
                public int Id { get; set; }
                public bool IsEnabled { get; set; }
                public bool IsCompleted { get; set; }
                public void Run() { }
                public void MarkCompleted() { }
            }
            """,
            """
            public class C
            {
                public void Execute(MyTask task)
                {
                    if (task is null || task.IsEnabled is false || task.IsCompleted)
                        return;

                    task.Run();
                    task.MarkCompleted();
                    Audit(task.Id);
                }

                private static void Audit(int id) { }
            }

            public class MyTask
            {
                public int Id { get; set; }
                public bool IsEnabled { get; set; }
                public bool IsCompleted { get; set; }
                public void Run() { }
                public void MarkCompleted() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_LiteralTrueToFalse()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    {|DL3002:if|} (true)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M()
                {
                    if (false)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_EqualsNullToIsNotNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? x)
                {
                    {|DL3002:if|} (x == null)
                    {
                        DoSomething();
                    }

                    throw new System.Exception();
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(object? x)
                {
                    if (x is not null)
                        throw new System.Exception();

                    DoSomething();

                    throw new System.Exception();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_IsNotNullToIsNull()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(object? x)
                {
                    {|DL3002:if|} (x is not null)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(object? x)
                {
                    if (x is null)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_IsTypeToIsNotType()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(object x)
                {
                    {|DL3002:if|} (x is string)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """,
            """
            public class C
            {
                public int M(object x)
                {
                    if (x is not string)
                        return 0;

                    return 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_ParenthesizedConditionStripsParens()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a)
                {
                    {|DL3002:if|} ((a))
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(bool a)
                {
                    if (a is false)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_OrToAnd()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    {|DL3002:if|} (a || b)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(bool a, bool b)
                {
                    if (a is false && b is false)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_MixedAndOrDeMorgan()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    {|DL3002:if|} ((a && b) || c)
                    {
                        DoSomething();
                    }
                }

                private static void DoSomething() { }
            }
            """,
            """
            public class C
            {
                public void M(bool a, bool b, bool c)
                {
                    if ((a is false || b is false) && c is false)
                        return;

                    DoSomething();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_RecursivePatternToNotPattern()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(object x)
                {
                    {|DL3002:if|} (x is string { Length: > 0 })
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """,
            """
            public class C
            {
                public int M(object x)
                {
                    if (x is not string { Length: > 0 })
                        return 0;

                    return 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_AwaitUsesLogicalNot()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    {|DL3002:if|} (await IsReadyAsync())
                    {
                        DoSomething();
                    }
                }

                private static Task<bool> IsReadyAsync() => Task.FromResult(true);
                private static void DoSomething() { }
            }
            """,
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M()
                {
                    if (!await IsReadyAsync())
                        return;

                    DoSomething();
                }

                private static Task<bool> IsReadyAsync() => Task.FromResult(true);
                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_LiteralFalseToTrue()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M()
                {
                    {|DL3002:if|} (false)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """,
            """
            public class C
            {
                public int M()
                {
                    if (true)
                        return 0;

                    return 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ConditionNegation_EqualsToNotEquals()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int M(object a, object b)
                {
                    {|DL3002:if|} (a == b)
                    {
                        return 1;
                    }

                    return 0;
                }
            }
            """,
            """
            public class C
            {
                public int M(object a, object b)
                {
                    if (a != b)
                        return 0;

                    return 1;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
