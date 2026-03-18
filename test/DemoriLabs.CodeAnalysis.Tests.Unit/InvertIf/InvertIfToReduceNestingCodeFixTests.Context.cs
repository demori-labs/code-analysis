namespace DemoriLabs.CodeAnalysis.Tests.Unit.InvertIf;

public partial class InvertIfToReduceNestingCodeFixTests
{
    [Test]
    public async Task Context_PatternMatchingWithDeclaration()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string Describe(object shape)
                {
                    {|DL3002:if|} (shape is Circle circle)
                    {
                        if (circle.Radius > 0)
                        {
                            return $"Circle with radius {circle.Radius}";
                        }
                    }

                    return "Unknown shape";
                }
            }

            public class Circle
            {
                public double Radius { get; set; }
            }
            """,
            """
            public class C
            {
                public string Describe(object shape)
                {
                    if (shape is not Circle circle)
                        return "Unknown shape";

                    if (circle.Radius <= 0)
                        return "Unknown shape";

                    return $"Circle with radius {circle.Radius}";
                }
            }

            public class Circle
            {
                public double Radius { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Context_PropertySetter()
    {
        var test = CreateTest(
            """
            public class C
            {
                private string _name;
                public string Name
                {
                    set
                    {
                        {|DL3002:if|} (value != null)
                        {
                            if (value.Length <= 100)
                            {
                                _name = value.Trim();
                            }
                        }
                    }
                }
            }
            """,
            """
            public class C
            {
                private string _name;
                public string Name
                {
                    set
                    {
                        if (value is null)
                            return;

                        if (value.Length > 100)
                            return;

                        _name = value.Trim();
                    }
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Context_Constructor()
    {
        var test = CreateTest(
            """
            public class UserProfile
            {
                public string Name { get; set; }
                public string Email { get; set; }
                public int Age { get; set; }
                public bool IsValid { get; set; }

                public UserProfile(string name, string email, int age)
                {
                    {|DL3002:if|} (!string.IsNullOrWhiteSpace(name))
                    {
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            if (age > 0 && age < 150)
                            {
                                Name = name;
                                Email = email;
                                Age = age;
                                IsValid = true;
                            }
                        }
                    }
                }
            }
            """,
            """
            public class UserProfile
            {
                public string Name { get; set; }
                public string Email { get; set; }
                public int Age { get; set; }
                public bool IsValid { get; set; }

                public UserProfile(string name, string email, int age)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        return;

                    if (string.IsNullOrWhiteSpace(email))
                        return;

                    if (age <= 0 || age >= 150)
                        return;

                    Name = name;
                    Email = email;
                    Age = age;
                    IsValid = true;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Context_DictionaryTryGetValue()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public string GetDisplayName(Dictionary<int, User> users, int id)
                {
                    {|DL3002:if|} (users.TryGetValue(id, out var user))
                    {
                        if (user.DisplayName != null)
                        {
                            return user.DisplayName;
                        }
                    }

                    return "Anonymous";
                }
            }

            public class User
            {
                public string DisplayName { get; set; }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public string GetDisplayName(Dictionary<int, User> users, int id)
                {
                    if (users.TryGetValue(id, out var user) is false)
                        return "Anonymous";

                    if (user.DisplayName is null)
                        return "Anonymous";

                    return user.DisplayName;
                }
            }

            public class User
            {
                public string DisplayName { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Context_OutVariables()
    {
        var test = CreateTest(
            """
            public class C
            {
                public decimal? ParsePrice(string input)
                {
                    {|DL3002:if|} (input != null)
                    {
                        if (decimal.TryParse(input, out var price))
                        {
                            if (price >= 0)
                            {
                                return price;
                            }
                        }
                    }

                    return null;
                }
            }
            """,
            """
            public class C
            {
                public decimal? ParsePrice(string input)
                {
                    if (input is null)
                        return null;

                    if (decimal.TryParse(input, out var price) is false)
                        return null;

                    if (price < 0)
                        return null;

                    return price;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Context_SwitchSection()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string Process(Command cmd)
                {
                    switch (cmd.Action)
                    {
                        case "save":
                            {|DL3002:if|} (cmd.Data != null)
                            {
                                if (cmd.Data.Length < 1_000_000)
                                {
                                    Save(cmd.Data);
                                    return "Saved";
                                }
                            }
                            return "Save failed";

                        case "delete":
                            {|DL3002:if|} (cmd.TargetId.HasValue)
                            {
                                if (Exists(cmd.TargetId.Value))
                                {
                                    Delete(cmd.TargetId.Value);
                                    return "Deleted";
                                }
                            }
                            return "Delete failed";

                        default:
                            return "Unknown action";
                    }
                }

                private static void Save(string data) { }
                private static bool Exists(int id) => true;
                private static void Delete(int id) { }
            }

            public class Command
            {
                public string Action { get; set; }
                public string Data { get; set; }
                public int? TargetId { get; set; }
            }
            """,
            """
            public class C
            {
                public string Process(Command cmd)
                {
                    switch (cmd.Action)
                    {
                        case "save":
                            if (cmd.Data is null)
                                return "Save failed";

                            if (cmd.Data.Length >= 1_000_000)
                                return "Save failed";

                            Save(cmd.Data);
                            return "Saved";

                        case "delete":
                            if (cmd.TargetId is null)
                                return "Delete failed";

                            if (Exists(cmd.TargetId.Value) is false)
                                return "Delete failed";

                            Delete(cmd.TargetId.Value);
                            return "Deleted";

                        default:
                            return "Unknown action";
                    }
                }

                private static void Save(string data) { }
                private static bool Exists(int id) => true;
                private static void Delete(int id) { }
            }

            public class Command
            {
                public string Action { get; set; }
                public string Data { get; set; }
                public int? TargetId { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Context_Lambda()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                public void RegisterCallbacks(EventBus bus)
                {
                    bus.On(new Action<OrderPlacedEvent>(e =>
                    {
                        {|DL3002:if|} (e.Order != null)
                        {
                            if (e.Order.Total > 500)
                            {
                                SendVipGift(e.Order.CustomerName);
                                LogVipOrder(e.Order);
                            }
                        }
                    }));
                }

                private static void SendVipGift(string name) { }
                private static void LogVipOrder(Order o) { }
            }

            public class EventBus
            {
                public void On(Action<OrderPlacedEvent> handler) { }
            }

            public class OrderPlacedEvent
            {
                public Order Order { get; set; }
            }

            public class Order
            {
                public decimal Total { get; set; }
                public string CustomerName { get; set; }
            }
            """,
            """
            using System;

            public class C
            {
                public void RegisterCallbacks(EventBus bus)
                {
                    bus.On(new Action<OrderPlacedEvent>(e =>
                    {
                        if (e.Order is null)
                            return;

                        if (e.Order.Total <= 500)
                            return;

                        SendVipGift(e.Order.CustomerName);
                        LogVipOrder(e.Order);
                    }));
                }

                private static void SendVipGift(string name) { }
                private static void LogVipOrder(Order o) { }
            }

            public class EventBus
            {
                public void On(Action<OrderPlacedEvent> handler) { }
            }

            public class OrderPlacedEvent
            {
                public Order Order { get; set; }
            }

            public class Order
            {
                public decimal Total { get; set; }
                public string CustomerName { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Context_AnonymousMethod()
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
                                Audit(message.Id);
                            }
                        }
                    };
                }

                private static void Handle(Message message) { }
                private static void Audit(int id) { }
            }

            public class Message
            {
                public int Id { get; set; }
                public bool IsValid { get; set; }
            }
            """,
            """
            using System;

            public class C
            {
                public void Register()
                {
                    Action<Message> handler = delegate(Message message)
                    {
                        if (message is null)
                            return;

                        if (message.IsValid is false)
                            return;

                        Handle(message);
                        Audit(message.Id);
                    };
                }

                private static void Handle(Message message) { }
                private static void Audit(int id) { }
            }

            public class Message
            {
                public int Id { get; set; }
                public bool IsValid { get; set; }
            }
            """
        );

        await test.RunAsync();
    }
}
