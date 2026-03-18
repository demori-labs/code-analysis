namespace DemoriLabs.CodeAnalysis.Tests.Unit.InvertIf;

public partial class InvertIfToReduceNestingCodeFixTests
{
    [Test]
    public async Task ExitStrategy_ReturnWithValue()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int Calculate(int? value)
                {
                    {|DL3002:if|} (value.HasValue)
                    {
                        var result = value.Value * 2;
                        return result + 1;
                    }

                    return -1;
                }
            }
            """,
            """
            public class C
            {
                public int Calculate(int? value)
                {
                    if (value is null)
                        return -1;

                    var result = value.Value * 2;
                    return result + 1;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_ContinueInForeach()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void NotifyUsers(List<User> users)
                {
                    foreach (var user in users)
                    {
                        {|DL3002:if|} (user.IsActive)
                        {
                            if (user.Email != null)
                            {
                                SendNotification(user.Email);
                                LogNotification(user.Id);
                            }
                        }
                    }
                }

                private static void SendNotification(string email) { }
                private static void LogNotification(int id) { }
            }

            public class User
            {
                public bool IsActive { get; set; }
                public string Email { get; set; }
                public int Id { get; set; }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public void NotifyUsers(List<User> users)
                {
                    foreach (var user in users)
                    {
                        if (user.IsActive is false)
                            continue;

                        if (user.Email is null)
                            continue;

                        SendNotification(user.Email);
                        LogNotification(user.Id);
                    }
                }

                private static void SendNotification(string email) { }
                private static void LogNotification(int id) { }
            }

            public class User
            {
                public bool IsActive { get; set; }
                public string Email { get; set; }
                public int Id { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_ContinueInFor()
    {
        var test = CreateTest(
            """
            public class C
            {
                public int SumPositiveEvens(int[] numbers)
                {
                    var sum = 0;
                    for (var i = 0; i < numbers.Length; i++)
                    {
                        {|DL3002:if|} (numbers[i] > 0)
                        {
                            if (numbers[i] % 2 == 0)
                            {
                                sum += numbers[i];
                            }
                        }
                    }
                    return sum;
                }
            }
            """,
            """
            public class C
            {
                public int SumPositiveEvens(int[] numbers)
                {
                    var sum = 0;
                    for (var i = 0; i < numbers.Length; i++)
                    {
                        if (numbers[i] <= 0)
                            continue;

                        if (numbers[i] % 2 is not 0)
                            continue;

                        sum += numbers[i];
                    }
                    return sum;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_ContinueInWhile()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void ProcessQueue(Queue<Job> queue)
                {
                    while (queue.Count > 0)
                    {
                        var job = queue.Dequeue();
                        {|DL3002:if|} (!job.IsCancelled)
                        {
                            if (job.IsReady)
                            {
                                job.Execute();
                                Log(job.Id);
                            }
                        }
                    }
                }

                private static void Log(int id) { }
            }

            public class Job
            {
                public int Id { get; set; }
                public bool IsCancelled { get; set; }
                public bool IsReady { get; set; }
                public void Execute() { }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public void ProcessQueue(Queue<Job> queue)
                {
                    while (queue.Count > 0)
                    {
                        var job = queue.Dequeue();
                        if (job.IsCancelled)
                            continue;

                        if (job.IsReady is false)
                            continue;

                        job.Execute();
                        Log(job.Id);
                    }
                }

                private static void Log(int id) { }
            }

            public class Job
            {
                public int Id { get; set; }
                public bool IsCancelled { get; set; }
                public bool IsReady { get; set; }
                public void Execute() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_IfElseFlipsToReturnElseBranch()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string Describe(Animal animal)
                {
                    {|DL3002:if|} (animal != null)
                    {
                        var name = animal.Name;
                        var sound = animal.GetSound();
                        var description = $"{name} says {sound}";
                        return description;
                    }
                    else
                    {
                        return "No animal";
                    }
                }
            }

            public class Animal
            {
                public string Name { get; set; }
                public string GetSound() => "moo";
            }
            """,
            """
            public class C
            {
                public string Describe(Animal animal)
                {
                    if (animal is null)
                        return "No animal";

                    var name = animal.Name;
                    var sound = animal.GetSound();
                    var description = $"{name} says {sound}";
                    return description;
                }
            }

            public class Animal
            {
                public string Name { get; set; }
                public string GetSound() => "moo";
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_ContinueInDoWhile()
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
                                Log(job.Id);
                            }
                        }
                    } while (queue.Count > 0);
                }

                private static void Handle(Job job) { }
                private static void Log(int id) { }
            }

            public class Job
            {
                public int Id { get; set; }
                public bool IsReady { get; set; }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public void ProcessQueue(Queue<Job> queue)
                {
                    do
                    {
                        var job = queue.Dequeue();
                        if (job is null)
                            continue;

                        if (job.IsReady is false)
                            continue;

                        Handle(job);
                        Log(job.Id);
                    } while (queue.Count > 0);
                }

                private static void Handle(Job job) { }
                private static void Log(int id) { }
            }

            public class Job
            {
                public int Id { get; set; }
                public bool IsReady { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_YieldBreak()
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
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public IEnumerable<int> Filter(int[] numbers, bool include)
                {
                    if (include is false)
                        yield break;

                    foreach (var number in numbers)
                    {
                        yield return number;
                    }

                    yield break;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_ThrowAsGuard()
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
                        DoMore();
                    }

                    throw new InvalidOperationException("failed");
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """,
            """
            using System;

            public class C
            {
                public void M(bool condition)
                {
                    if (condition is false)
                        throw new InvalidOperationException("failed");

                    DoSomething();
                    DoMore();

                    throw new InvalidOperationException("failed");
                }

                private static void DoSomething() { }
                private static void DoMore() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_ReturnInDestructor()
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
                        _resource = null;
                    }
                }

                private static void Cleanup(object r) { }
            }
            """,
            """
            public class C
            {
                private object? _resource;

                ~C()
                {
                    if (_resource is null)
                        return;

                    Cleanup(_resource);
                    _resource = null;
                }

                private static void Cleanup(object r) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task ExitStrategy_ReturnInLambda()
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
            """,
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
                        if (item is null)
                            return;

                        Process(item);
                    });
                }

                private static void Process(string s) { }
            }
            """
        );

        await test.RunAsync();
    }
}
