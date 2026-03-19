namespace DemoriLabs.CodeAnalysis.Tests.Unit.InvertIf;

// ReSharper disable MemberCanBeMadeStatic.Global
public partial class InvertIfToReduceNestingAnalyzerTests
{
    [Test]
    public async Task ExitContext_VoidMethod()
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
                    }
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_NonVoidMethodWithReturn()
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
    public async Task ExitContext_AsyncTaskMethod()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        await Task.Delay(1);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_AsyncValueTaskMethod()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async ValueTask M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        await Task.Delay(1);
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_AsyncTaskOfTMethod()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task<int> M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        return await Task.FromResult(42);
                    }

                    return 0;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_Constructor()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int _value;

                public C(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        _value = 42;
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_Destructor()
    {
        var test = CreateTest(
            """
            public class C
            {
                private object? _resource;

                ~C()
                {
                    {|DL3002:if|} (_resource != null)
                    {
                        Cleanup(_resource);
                    }
                }

                private static void Cleanup(object r) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_PropertySetter()
    {
        var test = CreateTest(
            """
            public class C
            {
                private int _value;

                public int Value
                {
                    get => _value;
                    set
                    {
                        {|DL3002:if|} (value > 0)
                        {
                            _value = value;
                        }
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_LocalFunction()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void M()
                {
                    void Inner(bool condition)
                    {
                        {|DL3002:if|} (condition)
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
    public async Task ExitContext_SimpleLambda()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public void M(List<string?> items)
                {
                    items.ForEach(item =>
                    {
                        {|DL3002:if|} (item != null)
                        {
                            Process(item);
                        }
                    });
                }

                private static void Process(string s) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_AnonymousMethod()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void Register()
                {
                    Action<Message> handler = delegate(Message message)
                    {
                        {|DL3002:if|} (message != null)
                        {
                            if (message.IsValid)
                            {
                                Handle(message);
                            }
                        }
                    };
                }

                private static void Handle(Message message) { }
            }

            public class Message
            {
                public bool IsValid { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_DoWhileLoop()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void ProcessQueue(Queue<Job> queue)
                {
                    do
                    {
                        var job = queue.Dequeue();
                        {|DL3002:if|} (job != null)
                        {
                            if (job.IsReady)
                            {
                                Handle(job);
                            }
                        }
                    } while (queue.Count > 0);
                }

                private static void Handle(Job job) { }
            }

            public class Job
            {
                public bool IsReady { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_FollowedByThrow()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void M(bool condition)
                {
                    {|DL3002:if|} (condition)
                    {
                        DoSomething();
                    }

                    throw new InvalidOperationException();
                }

                private static void DoSomething() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitContext_IteratorMethodWithYieldBreak()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public IEnumerable<int> Filter(int[] numbers, bool include)
                {
                    {|DL3002:if|} (include)
                    {
                        foreach (var number in numbers)
                        {
                            yield return number;
                        }
                    }

                    yield break;
                }
            }
            """
        );

        await test.RunAsync();
    }
}
