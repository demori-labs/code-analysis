namespace DemoriLabs.CodeAnalysis.Tests.Unit.InvertIf;

public partial class InvertIfToReduceNestingCodeFixTests
{
    [Test]
    public async Task Integration_ThreeLevelValidationChain()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string Format(Customer customer)
                {
                    {|DL3002:if|} (customer != null)
                    {
                        if (customer.Address != null)
                        {
                            if (!string.IsNullOrWhiteSpace(customer.Address.City))
                            {
                                return $"{customer.Name}, {customer.Address.City}";
                            }
                        }
                    }

                    return "Unknown";
                }
            }

            public class Customer
            {
                public string Name { get; set; }
                public Address Address { get; set; }
            }

            public class Address
            {
                public string City { get; set; }
            }
            """,
            """
            public class C
            {
                public string Format(Customer customer)
                {
                    if (customer is null)
                        return "Unknown";

                    if (customer.Address is null)
                        return "Unknown";

                    if (string.IsNullOrWhiteSpace(customer.Address.City))
                        return "Unknown";

                    return $"{customer.Name}, {customer.Address.City}";
                }
            }

            public class Customer
            {
                public string Name { get; set; }
                public Address Address { get; set; }
            }

            public class Address
            {
                public string City { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_ResourceAcquisitionWithThreeLevelNesting()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string ReadConfig(string path)
                {
                    {|DL3002:if|} (FileExists(path))
                    {
                        var text = ReadAllText(path);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var parsed = ParseConfig(text);
                            if (parsed != null)
                            {
                                return parsed.ToString();
                            }
                        }
                    }

                    return "Default";
                }

                private static bool FileExists(string p) => true;
                private static string ReadAllText(string p) => "";
                private static object ParseConfig(string t) => null;
            }
            """,
            """
            public class C
            {
                public string ReadConfig(string path)
                {
                    if (FileExists(path) is false)
                        return "Default";

                    var text = ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(text))
                        return "Default";

                    var parsed = ParseConfig(text);

                    if (parsed is null)
                        return "Default";

                    return parsed.ToString();
                }

                private static bool FileExists(string p) => true;
                private static string ReadAllText(string p) => "";
                private static object ParseConfig(string t) => null;
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_MultipleReturnValuesWithElseBranches()
    {
        var test = CreateTest(
            """
            public class C
            {
                public string Validate(Order order)
                {
                    {|DL3002:if|} (order != null)
                    {
                        if (order.ItemCount > 0)
                        {
                            if (order.Total > 0)
                            {
                                return "Success";
                            }
                            else
                            {
                                return "Total must be positive";
                            }
                        }
                        else
                        {
                            return "Order has no items";
                        }
                    }
                    else
                    {
                        return "Order is null";
                    }
                }
            }

            public class Order
            {
                public int ItemCount { get; set; }
                public decimal Total { get; set; }
            }
            """,
            """
            public class C
            {
                public string Validate(Order order)
                {
                    if (order is null)
                        return "Order is null";

                    if (order.ItemCount <= 0)
                        return "Order has no items";

                    if (order.Total <= 0)
                        return "Total must be positive";

                    return "Success";
                }
            }

            public class Order
            {
                public int ItemCount { get; set; }
                public decimal Total { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_AsyncMethodWithThreeLevelNesting()
    {
        var test = CreateTest(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> FetchDataAsync(HttpClient client, string url)
                {
                    {|DL3002:if|} (client != null)
                    {
                        if (!string.IsNullOrEmpty(url))
                        {
                            var response = await client.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                var content = await response.Content.ReadAsStringAsync();
                                return content;
                            }
                        }
                    }

                    return string.Empty;
                }
            }
            """,
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> FetchDataAsync(HttpClient client, string url)
                {
                    if (client is null)
                        return string.Empty;

                    if (string.IsNullOrEmpty(url))
                        return string.Empty;

                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode is false)
                        return string.Empty;

                    var content = await response.Content.ReadAsStringAsync();
                    return content;
                }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_LoopWithTypeCheckAndConditions()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public void ApplyDiscounts(List<object> items)
                {
                    foreach (var item in items)
                    {
                        {|DL3002:if|} (item is Product product)
                        {
                            if (product.IsEligibleForDiscount)
                            {
                                if (product.Price > 100)
                                {
                                    product.Price *= 0.9m;
                                    product.MarkDiscounted();
                                }
                            }
                        }
                    }
                }
            }

            public class Product
            {
                public bool IsEligibleForDiscount { get; set; }
                public decimal Price { get; set; }
                public void MarkDiscounted() { }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public void ApplyDiscounts(List<object> items)
                {
                    foreach (var item in items)
                    {
                        if (item is not Product product)
                            continue;

                        if (product.IsEligibleForDiscount is false)
                            continue;

                        if (product.Price <= 100)
                            continue;

                        product.Price *= 0.9m;
                        product.MarkDiscounted();
                    }
                }
            }

            public class Product
            {
                public bool IsEligibleForDiscount { get; set; }
                public decimal Price { get; set; }
                public void MarkDiscounted() { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_SideEffectsBetweenGuards()
    {
        var test = CreateTest(
            """
            public class C
            {
                public void ProcessTransaction(Account account, decimal amount)
                {
                    {|DL3002:if|} (account != null)
                    {
                        Log($"Processing for {account.Id}");
                        if (account.IsActive)
                        {
                            var balance = account.GetBalance();
                            if (balance >= amount)
                            {
                                account.Debit(amount);
                                Notify(account, amount);
                            }
                        }
                    }
                }

                private static void Log(string s) { }
                private static void Notify(Account a, decimal d) { }
            }

            public class Account
            {
                public int Id { get; set; }
                public bool IsActive { get; set; }
                public decimal GetBalance() => 0;
                public void Debit(decimal d) { }
            }
            """,
            """
            public class C
            {
                public void ProcessTransaction(Account account, decimal amount)
                {
                    if (account is null)
                        return;

                    Log($"Processing for {account.Id}");

                    if (account.IsActive is false)
                        return;

                    var balance = account.GetBalance();

                    if (balance < amount)
                        return;

                    account.Debit(amount);
                    Notify(account, amount);
                }

                private static void Log(string s) { }
                private static void Notify(Account a, decimal d) { }
            }

            public class Account
            {
                public int Id { get; set; }
                public bool IsActive { get; set; }
                public decimal GetBalance() => 0;
                public void Debit(decimal d) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_EventHandlerWithPatternMatching()
    {
        var test = CreateTest(
            """
            using System;

            public class C
            {
                private void OnButtonClick(object sender, EventArgs e)
                {
                    {|DL3002:if|} (sender is Button button)
                    {
                        if (button.IsEnabled)
                        {
                            if (button.Tag is string command)
                            {
                                ExecuteCommand(command);
                                UpdateStatus($"Executed: {command}");
                            }
                        }
                    }
                }

                private static void ExecuteCommand(string cmd) { }
                private static void UpdateStatus(string msg) { }
            }

            public class Button
            {
                public bool IsEnabled { get; set; }
                public object Tag { get; set; }
            }
            """,
            """
            using System;

            public class C
            {
                private void OnButtonClick(object sender, EventArgs e)
                {
                    if (sender is not Button button)
                        return;

                    if (button.IsEnabled is false)
                        return;

                    if (button.Tag is not string command)
                        return;

                    ExecuteCommand(command);
                    UpdateStatus($"Executed: {command}");
                }

                private static void ExecuteCommand(string cmd) { }
                private static void UpdateStatus(string msg) { }
            }

            public class Button
            {
                public bool IsEnabled { get; set; }
                public object Tag { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_ForeachWithMultipleGuards()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public List<Order> GetShippableOrders(List<Order> orders)
                {
                    var result = new List<Order>();
                    foreach (var order in orders)
                    {
                        {|DL3002:if|} (order.Status == OrderStatus.Paid)
                        {
                            if (order.ShippingAddress != null)
                            {
                                if (order.Items.All(i => i.InStock))
                                {
                                    result.Add(order);
                                }
                            }
                        }
                    }
                    return result;
                }
            }

            public enum OrderStatus { Paid, Pending }

            public class Order
            {
                public OrderStatus Status { get; set; }
                public string ShippingAddress { get; set; }
                public List<OrderItem> Items { get; set; } = new();
            }

            public class OrderItem
            {
                public bool InStock { get; set; }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public List<Order> GetShippableOrders(List<Order> orders)
                {
                    var result = new List<Order>();
                    foreach (var order in orders)
                    {
                        if (order.Status is not OrderStatus.Paid)
                            continue;

                        if (order.ShippingAddress is null)
                            continue;

                        if (order.Items.All(i => i.InStock) is false)
                            continue;

                        result.Add(order);
                    }
                    return result;
                }
            }

            public enum OrderStatus { Paid, Pending }

            public class Order
            {
                public OrderStatus Status { get; set; }
                public string ShippingAddress { get; set; }
                public List<OrderItem> Items { get; set; } = new();
            }

            public class OrderItem
            {
                public bool InStock { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_FiveLevelAsyncValidationPipeline()
    {
        var test = CreateTest(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<Result> SubmitClaimAsync(Claim claim)
                {
                    {|DL3002:if|} (claim != null)
                    {
                        if (claim.PolicyId != Guid.Empty)
                        {
                            var policy = await GetPolicyAsync(claim.PolicyId);
                            if (policy != null)
                            {
                                if (policy.IsActive)
                                {
                                    if (claim.Amount > 0 && claim.Amount <= policy.MaxCoverage)
                                    {
                                        var reference = await CreateClaimAsync(claim);
                                        await NotifyAdjusterAsync(reference);
                                        return Result.Success(reference);
                                    }
                                    else
                                    {
                                        return Result.Failure("Amount out of range");
                                    }
                                }
                                else
                                {
                                    return Result.Failure("Policy is inactive");
                                }
                            }
                            else
                            {
                                return Result.Failure("Policy not found");
                            }
                        }
                        else
                        {
                            return Result.Failure("Invalid policy ID");
                        }
                    }
                    else
                    {
                        return Result.Failure("Claim is null");
                    }
                }

                private static Task<Policy> GetPolicyAsync(Guid id) => Task.FromResult(new Policy());
                private static Task<string> CreateClaimAsync(Claim c) => Task.FromResult("ref");
                private static Task NotifyAdjusterAsync(string r) => Task.CompletedTask;
            }

            public class Claim
            {
                public Guid PolicyId { get; set; }
                public decimal Amount { get; set; }
            }

            public class Policy
            {
                public bool IsActive { get; set; }
                public decimal MaxCoverage { get; set; }
            }

            public class Result
            {
                public static Result Success(string r) => new();
                public static Result Failure(string msg) => new();
            }
            """,
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<Result> SubmitClaimAsync(Claim claim)
                {
                    if (claim is null)
                        return Result.Failure("Claim is null");

                    if (claim.PolicyId == Guid.Empty)
                        return Result.Failure("Invalid policy ID");

                    var policy = await GetPolicyAsync(claim.PolicyId);

                    if (policy is null)
                        return Result.Failure("Policy not found");

                    if (policy.IsActive is false)
                        return Result.Failure("Policy is inactive");

                    if (claim.Amount <= 0 || claim.Amount > policy.MaxCoverage)
                        return Result.Failure("Amount out of range");

                    var reference = await CreateClaimAsync(claim);
                    await NotifyAdjusterAsync(reference);
                    return Result.Success(reference);
                }

                private static Task<Policy> GetPolicyAsync(Guid id) => Task.FromResult(new Policy());
                private static Task<string> CreateClaimAsync(Claim c) => Task.FromResult("ref");
                private static Task NotifyAdjusterAsync(string r) => Task.CompletedTask;
            }

            public class Claim
            {
                public Guid PolicyId { get; set; }
                public decimal Amount { get; set; }
            }

            public class Policy
            {
                public bool IsActive { get; set; }
                public decimal MaxCoverage { get; set; }
            }

            public class Result
            {
                public static Result Success(string r) => new();
                public static Result Failure(string msg) => new();
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_LoopWithAccumulatorAndFourLevelNesting()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public decimal CalculateTotalDiscount(List<CartItem> items, Customer customer)
                {
                    var total = 0m;
                    foreach (var item in items)
                    {
                        {|DL3002:if|} (item.IsDiscountable)
                        {
                            if (customer.MembershipLevel != MembershipLevel.None)
                            {
                                if (item.Quantity > 0)
                                {
                                    var rate = GetDiscountRate(customer.MembershipLevel);
                                    var discount = item.Price * item.Quantity * rate;
                                    if (discount > 0)
                                    {
                                        total += discount;
                                    }
                                }
                            }
                        }
                    }
                    return total;
                }

                private static decimal GetDiscountRate(MembershipLevel level) => 0.1m;
            }

            public enum MembershipLevel { None, Silver, Gold }

            public class CartItem
            {
                public bool IsDiscountable { get; set; }
                public int Quantity { get; set; }
                public decimal Price { get; set; }
            }

            public class Customer
            {
                public MembershipLevel MembershipLevel { get; set; }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public decimal CalculateTotalDiscount(List<CartItem> items, Customer customer)
                {
                    var total = 0m;
                    foreach (var item in items)
                    {
                        if (item.IsDiscountable is false)
                            continue;

                        if (customer.MembershipLevel is MembershipLevel.None)
                            continue;

                        if (item.Quantity <= 0)
                            continue;

                        var rate = GetDiscountRate(customer.MembershipLevel);
                        var discount = item.Price * item.Quantity * rate;

                        if (discount <= 0)
                            continue;

                        total += discount;
                    }
                    return total;
                }

                private static decimal GetDiscountRate(MembershipLevel level) => 0.1m;
            }

            public enum MembershipLevel { None, Silver, Gold }

            public class CartItem
            {
                public bool IsDiscountable { get; set; }
                public int Quantity { get; set; }
                public decimal Price { get; set; }
            }

            public class Customer
            {
                public MembershipLevel MembershipLevel { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_IntermediateVariablesBetweenGuards()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly Repository _repository = new();

                public string BuildReport(int projectId)
                {
                    {|DL3002:if|} (projectId > 0)
                    {
                        var project = _repository.GetProject(projectId);
                        if (project != null)
                        {
                            var tasks = _repository.GetTasks(project.Id);
                            if (tasks.Count > 0)
                            {
                                var completedCount = tasks.Count(t => t.IsCompleted);
                                var percentage = (double)completedCount / tasks.Count * 100;
                                var report = $"{project.Name}: {percentage:F1}% complete ({completedCount}/{tasks.Count})";
                                return report;
                            }
                        }
                    }

                    return "No data available";
                }
            }

            public class Repository
            {
                public Project GetProject(int id) => null;
                public List<TaskItem> GetTasks(int id) => new();
            }

            public class Project
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }

            public class TaskItem
            {
                public bool IsCompleted { get; set; }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly Repository _repository = new();

                public string BuildReport(int projectId)
                {
                    if (projectId <= 0)
                        return "No data available";

                    var project = _repository.GetProject(projectId);

                    if (project is null)
                        return "No data available";

                    var tasks = _repository.GetTasks(project.Id);

                    if (tasks.Count <= 0)
                        return "No data available";

                    var completedCount = tasks.Count(t => t.IsCompleted);
                    var percentage = (double)completedCount / tasks.Count * 100;
                    var report = $"{project.Name}: {percentage:F1}% complete ({completedCount}/{tasks.Count})";
                    return report;
                }
            }

            public class Repository
            {
                public Project GetProject(int id) => null;
                public List<TaskItem> GetTasks(int id) => new();
            }

            public class Project
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }

            public class TaskItem
            {
                public bool IsCompleted { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_FiveLevelAsyncAuthorizationPipeline()
    {
        var test = CreateTest(
            """
            using System.Threading.Tasks;
            using System.Linq;

            public class C
            {
                private readonly IAuthService _authService = null!;
                private readonly IHandler _handler = null!;

                public async Task<ApiResponse> ProcessRequestAsync(HttpContext context)
                {
                    {|DL3002:if|} (context.Request.Method == "POST")
                    {
                        var token = context.Request.Headers["Authorization"].FirstOrDefault();
                        if (token != null)
                        {
                            var principal = await _authService.ValidateTokenAsync(token);
                            if (principal != null)
                            {
                                if (principal.HasPermission("write"))
                                {
                                    var body = await ReadBodyAsync(context.Request);
                                    if (body != null)
                                    {
                                        var result = await _handler.HandleAsync(body, principal);
                                        return new ApiResponse(200, result);
                                    }
                                    else
                                    {
                                        return new ApiResponse(400, "Empty body");
                                    }
                                }
                                else
                                {
                                    return new ApiResponse(403, "Forbidden");
                                }
                            }
                            else
                            {
                                return new ApiResponse(401, "Invalid token");
                            }
                        }
                        else
                        {
                            return new ApiResponse(401, "Missing token");
                        }
                    }
                    else
                    {
                        return new ApiResponse(405, "Method not allowed");
                    }
                }

                private static Task<string> ReadBodyAsync(HttpRequest r) => Task.FromResult("body");
            }

            public class HttpContext { public HttpRequest Request { get; set; } = null!; }
            public class HttpRequest
            {
                public string Method { get; set; } = "";
                public Headers Headers { get; set; } = null!;
            }
            public class Headers
            {
                public string[] this[string key] => new[] { "" };
            }
            public interface IAuthService { Task<Principal> ValidateTokenAsync(string token); }
            public interface IHandler { Task<string> HandleAsync(string body, Principal p); }
            public class Principal { public bool HasPermission(string p) => true; }
            public class ApiResponse
            {
                public ApiResponse(int code, string body) { }
            }
            """,
            """
            using System.Threading.Tasks;
            using System.Linq;

            public class C
            {
                private readonly IAuthService _authService = null!;
                private readonly IHandler _handler = null!;

                public async Task<ApiResponse> ProcessRequestAsync(HttpContext context)
                {
                    if (context.Request.Method is not "POST")
                        return new ApiResponse(405, "Method not allowed");

                    var token = context.Request.Headers["Authorization"].FirstOrDefault();

                    if (token is null)
                        return new ApiResponse(401, "Missing token");

                    var principal = await _authService.ValidateTokenAsync(token);

                    if (principal is null)
                        return new ApiResponse(401, "Invalid token");

                    if (principal.HasPermission("write") is false)
                        return new ApiResponse(403, "Forbidden");

                    var body = await ReadBodyAsync(context.Request);

                    if (body is null)
                        return new ApiResponse(400, "Empty body");

                    var result = await _handler.HandleAsync(body, principal);
                    return new ApiResponse(200, result);
                }

                private static Task<string> ReadBodyAsync(HttpRequest r) => Task.FromResult("body");
            }

            public class HttpContext { public HttpRequest Request { get; set; } = null!; }
            public class HttpRequest
            {
                public string Method { get; set; } = "";
                public Headers Headers { get; set; } = null!;
            }
            public class Headers
            {
                public string[] this[string key] => new[] { "" };
            }
            public interface IAuthService { Task<Principal> ValidateTokenAsync(string token); }
            public interface IHandler { Task<string> HandleAsync(string body, Principal p); }
            public class Principal { public bool HasPermission(string p) => true; }
            public class ApiResponse
            {
                public ApiResponse(int code, string body) { }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_SixLevelECommerceCheckout()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class C
            {
                private readonly IInventoryService _inventoryService = null!;
                private readonly IPaymentGateway _paymentGateway = null!;
                private readonly IOrderService _orderService = null!;
                private readonly IEmailService _emailService = null!;

                public async Task<CheckoutResult> CheckoutAsync(Cart cart, PaymentInfo payment, Address shipping)
                {
                    {|DL3002:if|} (cart != null)
                    {
                        if (cart.Items.Count > 0)
                        {
                            if (payment != null)
                            {
                                if (payment.IsValid())
                                {
                                    if (shipping != null)
                                    {
                                        if (await _inventoryService.ReserveAllAsync(cart.Items))
                                        {
                                            var total = cart.CalculateTotal();
                                            var charge = await _paymentGateway.ChargeAsync(payment, total);
                                            if (charge.Succeeded)
                                            {
                                                var order = await _orderService.CreateAsync(cart, shipping, charge.TransactionId);
                                                await _emailService.SendConfirmationAsync(order);
                                                return CheckoutResult.Success(order.Id);
                                            }
                                            else
                                            {
                                                await _inventoryService.ReleaseAllAsync(cart.Items);
                                                return CheckoutResult.Failure($"Payment failed: {charge.Error}");
                                            }
                                        }
                                        else
                                        {
                                            return CheckoutResult.Failure("Some items are out of stock");
                                        }
                                    }
                                    else
                                    {
                                        return CheckoutResult.Failure("Shipping address required");
                                    }
                                }
                                else
                                {
                                    return CheckoutResult.Failure("Invalid payment information");
                                }
                            }
                            else
                            {
                                return CheckoutResult.Failure("Payment information required");
                            }
                        }
                        else
                        {
                            return CheckoutResult.Failure("Cart is empty");
                        }
                    }
                    else
                    {
                        return CheckoutResult.Failure("Cart is null");
                    }
                }
            }

            public class Cart
            {
                public List<CartItem> Items { get; set; } = new();
                public decimal CalculateTotal() => 0m;
            }
            public class CartItem { }
            public class PaymentInfo { public bool IsValid() => true; }
            public class Address { }
            public interface IInventoryService
            {
                Task<bool> ReserveAllAsync(List<CartItem> items);
                Task ReleaseAllAsync(List<CartItem> items);
            }
            public interface IPaymentGateway
            {
                Task<ChargeResult> ChargeAsync(PaymentInfo p, decimal total);
            }
            public interface IOrderService
            {
                Task<OrderResult> CreateAsync(Cart c, Address a, string txnId);
            }
            public interface IEmailService { Task SendConfirmationAsync(OrderResult o); }
            public class ChargeResult
            {
                public bool Succeeded { get; set; }
                public string TransactionId { get; set; } = "";
                public string Error { get; set; } = "";
            }
            public class OrderResult { public string Id { get; set; } = ""; }
            public class CheckoutResult
            {
                public static CheckoutResult Success(string id) => new();
                public static CheckoutResult Failure(string msg) => new();
            }
            """,
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class C
            {
                private readonly IInventoryService _inventoryService = null!;
                private readonly IPaymentGateway _paymentGateway = null!;
                private readonly IOrderService _orderService = null!;
                private readonly IEmailService _emailService = null!;

                public async Task<CheckoutResult> CheckoutAsync(Cart cart, PaymentInfo payment, Address shipping)
                {
                    if (cart is null)
                        return CheckoutResult.Failure("Cart is null");

                    if (cart.Items.Count <= 0)
                        return CheckoutResult.Failure("Cart is empty");

                    if (payment is null)
                        return CheckoutResult.Failure("Payment information required");

                    if (payment.IsValid() is false)
                        return CheckoutResult.Failure("Invalid payment information");

                    if (shipping is null)
                        return CheckoutResult.Failure("Shipping address required");

                    if (!await _inventoryService.ReserveAllAsync(cart.Items))
                        return CheckoutResult.Failure("Some items are out of stock");

                    var total = cart.CalculateTotal();
                    var charge = await _paymentGateway.ChargeAsync(payment, total);

                    if (charge.Succeeded is false)
                    {
                        await _inventoryService.ReleaseAllAsync(cart.Items);
                        return CheckoutResult.Failure($"Payment failed: {charge.Error}");
                    }

                    var order = await _orderService.CreateAsync(cart, shipping, charge.TransactionId);
                    await _emailService.SendConfirmationAsync(order);
                    return CheckoutResult.Success(order.Id);
                }
            }

            public class Cart
            {
                public List<CartItem> Items { get; set; } = new();
                public decimal CalculateTotal() => 0m;
            }
            public class CartItem { }
            public class PaymentInfo { public bool IsValid() => true; }
            public class Address { }
            public interface IInventoryService
            {
                Task<bool> ReserveAllAsync(List<CartItem> items);
                Task ReleaseAllAsync(List<CartItem> items);
            }
            public interface IPaymentGateway
            {
                Task<ChargeResult> ChargeAsync(PaymentInfo p, decimal total);
            }
            public interface IOrderService
            {
                Task<OrderResult> CreateAsync(Cart c, Address a, string txnId);
            }
            public interface IEmailService { Task SendConfirmationAsync(OrderResult o); }
            public class ChargeResult
            {
                public bool Succeeded { get; set; }
                public string TransactionId { get; set; } = "";
                public string Error { get; set; } = "";
            }
            public class OrderResult { public string Id { get; set; } = ""; }
            public class CheckoutResult
            {
                public static CheckoutResult Success(string id) => new();
                public static CheckoutResult Failure(string msg) => new();
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_ForeachWithYieldReturnAndFourLevelNesting()
    {
        var test = CreateTest(
            """
            using System.Collections.Generic;

            public class C
            {
                public IEnumerable<string> GetValidEmails(IEnumerable<Contact> contacts)
                {
                    foreach (var contact in contacts)
                    {
                        {|DL3002:if|} (contact != null)
                        {
                            if (contact.IsActive)
                            {
                                if (!string.IsNullOrWhiteSpace(contact.Email))
                                {
                                    if (contact.Email.Contains('@'))
                                    {
                                        yield return contact.Email;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public class Contact
            {
                public bool IsActive { get; set; }
                public string Email { get; set; }
            }
            """,
            """
            using System.Collections.Generic;

            public class C
            {
                public IEnumerable<string> GetValidEmails(IEnumerable<Contact> contacts)
                {
                    foreach (var contact in contacts)
                    {
                        if (contact is null)
                            continue;

                        if (contact.IsActive is false)
                            continue;

                        if (string.IsNullOrWhiteSpace(contact.Email))
                            continue;

                        if (contact.Email.Contains('@') is false)
                            continue;

                        yield return contact.Email;
                    }
                }
            }

            public class Contact
            {
                public bool IsActive { get; set; }
                public string Email { get; set; }
            }
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task Integration_FiveLevelGuardChainWithComputation()
    {
        var test = CreateTest(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public static TimeSpan? CalculateEstimate(Project project, Team team)
                {
                    {|DL3002:if|} (project != null)
                    {
                        if (team != null)
                        {
                            if (team.MemberCount > 0)
                            {
                                if (project.StoryPoints > 0)
                                {
                                    var velocity = team.GetAverageVelocity();
                                    if (velocity > 0)
                                    {
                                        var sprints = (double)project.StoryPoints / velocity;
                                        return TimeSpan.FromDays(sprints * team.SprintLengthDays);
                                    }
                                }
                            }
                        }
                    }

                    return null;
                }
            }

            public class Project
            {
                public int StoryPoints { get; set; }
            }

            public class Team
            {
                public int MemberCount { get; set; }
                public int SprintLengthDays { get; set; }
                public double GetAverageVelocity() => 0;
            }
            """,
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public static TimeSpan? CalculateEstimate(Project project, Team team)
                {
                    if (project is null)
                        return null;

                    if (team is null)
                        return null;

                    if (team.MemberCount <= 0)
                        return null;

                    if (project.StoryPoints <= 0)
                        return null;

                    var velocity = team.GetAverageVelocity();

                    if (velocity <= 0)
                        return null;

                    var sprints = (double)project.StoryPoints / velocity;
                    return TimeSpan.FromDays(sprints * team.SprintLengthDays);
                }
            }

            public class Project
            {
                public int StoryPoints { get; set; }
            }

            public class Team
            {
                public int MemberCount { get; set; }
                public int SprintLengthDays { get; set; }
                public double GetAverageVelocity() => 0;
            }
            """
        );

        await test.RunAsync();
    }
}
