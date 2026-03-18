# Invert-If Refactoring — Test Cases

> Each example shows **Actual** (nested code) and **Expected** (refactored code with reduced nesting).
> The refactoring strategy is: invert the condition and return/continue/break early, eliminating unnecessary nesting.

---

## 1. Simple Guard Clause — Single `if` Wrapping a Method Body

### Actual

```csharp
public void Process(Order order)
{
    if (order != null)
    {
        order.Validate();
        order.Submit();
    }
}
```

### Expected

```csharp
public void Process(Order order)
{
    if (order is null)
        return;

    order.Validate();
    order.Submit();
}
```

---

## 2. Simple Guard Clause — Negated Condition

### Actual

```csharp
public void SendEmail(string address)
{
    if (!string.IsNullOrEmpty(address))
    {
        var client = new SmtpClient();
        client.Send(address, "Hello");
    }
}
```

### Expected

```csharp
public void SendEmail(string address)
{
    if (string.IsNullOrEmpty(address))
        return;

    var client = new SmtpClient();
    client.Send(address, "Hello");
}
```

---

## 3. Guard Clause With Return Value

### Actual

```csharp
public int Calculate(int? value)
{
    if (value.HasValue)
    {
        var result = value.Value * 2;
        return result + 1;
    }

    return -1;
}
```

### Expected

```csharp
public int Calculate(int? value)
{
    if (!value.HasValue)
        return -1;

    var result = value.Value * 2;
    return result + 1;
}
```

---

## 4. Two Sequential Null Checks — Nested

### Actual

```csharp
public void Handle(Request request)
{
    if (request != null)
    {
        if (request.Payload != null)
        {
            request.Payload.Process();
            Log("Processed");
        }
    }
}
```

### Expected

```csharp
public void Handle(Request request)
{
    if (request is null)
        return;

    if (request.Payload is null)
        return;

    request.Payload.Process();
    Log("Processed");
}
```

---

## 5. Three-Level Nesting — Validation Chain

### Actual

```csharp
public string Format(Customer customer)
{
    if (customer != null)
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
```

### Expected

```csharp
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
```

---

## 6. `if` Inside a `foreach` Loop — `continue`

### Actual

```csharp
public void NotifyUsers(List<User> users)
{
    foreach (var user in users)
    {
        if (user.IsActive)
        {
            if (user.Email != null)
            {
                SendNotification(user.Email);
                LogNotification(user.Id);
            }
        }
    }
}
```

### Expected

```csharp
public void NotifyUsers(List<User> users)
{
    foreach (var user in users)
    {
        if (!user.IsActive)
            continue;

        if (user.Email is null)
            continue;

        SendNotification(user.Email);
        LogNotification(user.Id);
    }
}
```

---

## 7. `if` Inside a `for` Loop — `continue`

### Actual

```csharp
public int SumPositiveEvens(int[] numbers)
{
    var sum = 0;
    for (var i = 0; i < numbers.Length; i++)
    {
        if (numbers[i] > 0)
        {
            if (numbers[i] % 2 == 0)
            {
                sum += numbers[i];
            }
        }
    }
    return sum;
}
```

### Expected

```csharp
public int SumPositiveEvens(int[] numbers)
{
    var sum = 0;
    for (var i = 0; i < numbers.Length; i++)
    {
        if (numbers[i] <= 0)
            continue;

        if (numbers[i] % 2 != 0)
            continue;

        sum += numbers[i];
    }
    return sum;
}
```

---

## 8. `if` Inside a `while` Loop — `continue`

### Actual

```csharp
public void ProcessQueue(Queue<Job> queue)
{
    while (queue.Count > 0)
    {
        var job = queue.Dequeue();
        if (!job.IsCancelled)
        {
            if (job.IsReady)
            {
                job.Execute();
                Log(job.Id);
            }
        }
    }
}
```

### Expected

```csharp
public void ProcessQueue(Queue<Job> queue)
{
    while (queue.Count > 0)
    {
        var job = queue.Dequeue();

        if (job.IsCancelled)
            continue;

        if (!job.IsReady)
            continue;

        job.Execute();
        Log(job.Id);
    }
}
```

---

## 9. `if-else` Where the `if` Branch Is the Larger Block

### Actual

```csharp
public string Describe(Animal animal)
{
    if (animal != null)
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
```

### Expected

```csharp
public string Describe(Animal animal)
{
    if (animal is null)
        return "No animal";

    var name = animal.Name;
    var sound = animal.GetSound();
    var description = $"{name} says {sound}";
    return description;
}
```

---

## 10. Compound Boolean Condition

### Actual

```csharp
public void Execute(Task task)
{
    if (task != null && task.IsEnabled && !task.IsCompleted)
    {
        task.Run();
        task.MarkCompleted();
        Audit(task.Id);
    }
}
```

### Expected

```csharp
public void Execute(Task task)
{
    if (task is null || !task.IsEnabled || task.IsCompleted)
        return;

    task.Run();
    task.MarkCompleted();
    Audit(task.Id);
}
```

---

## 11. Nested `if` With `else` at Inner Level Only

### Actual

```csharp
public string Classify(int score)
{
    if (score >= 0)
    {
        if (score >= 50)
        {
            return "Pass";
        }
        else
        {
            return "Fail";
        }
    }

    return "Invalid";
}
```

### Expected

```csharp
public string Classify(int score)
{
    if (score < 0)
        return "Invalid";

    if (score >= 50)
        return "Pass";

    return "Fail";
}
```

---

## 12. Boolean Flag Condition Inside Loop

### Actual

```csharp
public List<string> FilterNames(List<Person> people, bool onlyAdults)
{
    var result = new List<string>();
    foreach (var person in people)
    {
        if (onlyAdults)
        {
            if (person.Age >= 18)
            {
                result.Add(person.Name);
            }
        }
        else
        {
            result.Add(person.Name);
        }
    }
    return result;
}
```

### Expected

```csharp
public List<string> FilterNames(List<Person> people, bool onlyAdults)
{
    var result = new List<string>();
    foreach (var person in people)
    {
        if (onlyAdults && person.Age < 18)
            continue;

        result.Add(person.Name);
    }
    return result;
}
```

---

## 13. Nested `if` After Resource Acquisition (Using Pattern)

### Actual

```csharp
public string ReadConfig(string path)
{
    if (File.Exists(path))
    {
        var text = File.ReadAllText(path);
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
```

### Expected

```csharp
public string ReadConfig(string path)
{
    if (!File.Exists(path))
        return "Default";

    var text = File.ReadAllText(path);
    if (string.IsNullOrWhiteSpace(text))
        return "Default";

    var parsed = ParseConfig(text);
    if (parsed is null)
        return "Default";

    return parsed.ToString();
}
```

---

## 14. Try-Catch Inside Nested `if`

### Actual

```csharp
public void ImportData(string connectionString)
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        if (CanConnect(connectionString))
        {
            try
            {
                var data = FetchData(connectionString);
                Save(data);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }
    }
}
```

### Expected

```csharp
public void ImportData(string connectionString)
{
    if (string.IsNullOrEmpty(connectionString))
        return;

    if (!CanConnect(connectionString))
        return;

    try
    {
        var data = FetchData(connectionString);
        Save(data);
    }
    catch (Exception ex)
    {
        LogError(ex);
    }
}
```

---

## 15. `if` Wrapping a `using` Block

### Actual

```csharp
public void WriteLog(string message)
{
    if (message != null)
    {
        using (var writer = new StreamWriter("log.txt", true))
        {
            writer.WriteLine(message);
        }
    }
}
```

### Expected

```csharp
public void WriteLog(string message)
{
    if (message is null)
        return;

    using var writer = new StreamWriter("log.txt", true);
    writer.WriteLine(message);
}
```

---

## 16. `if` Wrapping a `lock` Block

### Actual

```csharp
public void UpdateCache(string key, object value)
{
    if (key != null)
    {
        if (value != null)
        {
            lock (_syncRoot)
            {
                _cache[key] = value;
                _timestamps[key] = DateTime.UtcNow;
            }
        }
    }
}
```

### Expected

```csharp
public void UpdateCache(string key, object value)
{
    if (key is null)
        return;

    if (value is null)
        return;

    lock (_syncRoot)
    {
        _cache[key] = value;
        _timestamps[key] = DateTime.UtcNow;
    }
}
```

---

## 17. Nested `if` With Multiple Return Values

### Actual

```csharp
public Result Validate(Order order)
{
    if (order != null)
    {
        if (order.Items.Count > 0)
        {
            if (order.Total > 0)
            {
                return Result.Success();
            }
            else
            {
                return Result.Failure("Total must be positive");
            }
        }
        else
        {
            return Result.Failure("Order has no items");
        }
    }
    else
    {
        return Result.Failure("Order is null");
    }
}
```

### Expected

```csharp
public Result Validate(Order order)
{
    if (order is null)
        return Result.Failure("Order is null");

    if (order.Items.Count <= 0)
        return Result.Failure("Order has no items");

    if (order.Total <= 0)
        return Result.Failure("Total must be positive");

    return Result.Success();
}
```

---

## 18. Pattern Matching With Nested `if`

### Actual

```csharp
public string Describe(object shape)
{
    if (shape is Circle circle)
    {
        if (circle.Radius > 0)
        {
            return $"Circle with radius {circle.Radius}";
        }
    }

    return "Unknown shape";
}
```

### Expected

```csharp
public string Describe(object shape)
{
    if (shape is not Circle circle)
        return "Unknown shape";

    if (circle.Radius <= 0)
        return "Unknown shape";

    return $"Circle with radius {circle.Radius}";
}
```

---

## 19. Deeply Nested Pattern Matching Chain

### Actual

```csharp
public double CalculateArea(object shape)
{
    if (shape is Shape s)
    {
        if (s is Circle c)
        {
            if (c.Radius > 0)
            {
                return Math.PI * c.Radius * c.Radius;
            }
        }
        else if (s is Rectangle r)
        {
            if (r.Width > 0 && r.Height > 0)
            {
                return r.Width * r.Height;
            }
        }
    }

    return 0;
}
```

### Expected

```csharp
public double CalculateArea(object shape)
{
    if (shape is not Shape s)
        return 0;

    if (s is Circle c)
    {
        if (c.Radius <= 0)
            return 0;

        return Math.PI * c.Radius * c.Radius;
    }

    if (s is Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0)
            return 0;

        return r.Width * r.Height;
    }

    return 0;
}
```

---

## 20. Async Method With Nested Checks

### Actual

```csharp
public async Task<string> FetchDataAsync(HttpClient client, string url)
{
    if (client != null)
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
```

### Expected

```csharp
public async Task<string> FetchDataAsync(HttpClient client, string url)
{
    if (client is null)
        return string.Empty;

    if (string.IsNullOrEmpty(url))
        return string.Empty;

    var response = await client.GetAsync(url);
    if (!response.IsSuccessStatusCode)
        return string.Empty;

    var content = await response.Content.ReadAsStringAsync();
    return content;
}
```

---

## 21. Loop With Nested Type Check and Condition

### Actual

```csharp
public void ApplyDiscounts(List<object> items)
{
    foreach (var item in items)
    {
        if (item is Product product)
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
```

### Expected

```csharp
public void ApplyDiscounts(List<object> items)
{
    foreach (var item in items)
    {
        if (item is not Product product)
            continue;

        if (!product.IsEligibleForDiscount)
            continue;

        if (product.Price <= 100)
            continue;

        product.Price *= 0.9m;
        product.MarkDiscounted();
    }
}
```

---

## 22. Nested `if` With Side Effects Before the Inner Block

### Actual

```csharp
public void ProcessTransaction(Account account, decimal amount)
{
    if (account != null)
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
```

### Expected

```csharp
public void ProcessTransaction(Account account, decimal amount)
{
    if (account is null)
        return;

    Log($"Processing for {account.Id}");

    if (!account.IsActive)
        return;

    var balance = account.GetBalance();
    if (balance < amount)
        return;

    account.Debit(amount);
    Notify(account, amount);
}
```

---

## 23. Nested `if` in a Property Setter

### Actual

```csharp
private string _name;
public string Name
{
    set
    {
        if (value != null)
        {
            if (value.Length <= 100)
            {
                _name = value.Trim();
            }
        }
    }
}
```

### Expected

```csharp
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
```

---

## 24. Constructor With Nested Validation

### Actual

```csharp
public UserProfile(string name, string email, int age)
{
    if (!string.IsNullOrWhiteSpace(name))
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
```

### Expected

```csharp
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
```

---

## 25. Event Handler With Nested Checks

### Actual

```csharp
private void OnButtonClick(object sender, EventArgs e)
{
    if (sender is Button button)
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
```

### Expected

```csharp
private void OnButtonClick(object sender, EventArgs e)
{
    if (sender is not Button button)
        return;

    if (!button.IsEnabled)
        return;

    if (button.Tag is not string command)
        return;

    ExecuteCommand(command);
    UpdateStatus($"Executed: {command}");
}
```

---

## 26. LINQ Predicate Extraction — Nested `if` in a `Where` Delegate

### Actual

```csharp
public List<Order> GetShippableOrders(List<Order> orders)
{
    var result = new List<Order>();
    foreach (var order in orders)
    {
        if (order.Status == OrderStatus.Paid)
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
```

### Expected

```csharp
public List<Order> GetShippableOrders(List<Order> orders)
{
    var result = new List<Order>();
    foreach (var order in orders)
    {
        if (order.Status != OrderStatus.Paid)
            continue;

        if (order.ShippingAddress is null)
            continue;

        if (!order.Items.All(i => i.InStock))
            continue;

        result.Add(order);
    }
    return result;
}
```

---

## 27. Dictionary Lookup With Nested `if`

### Actual

```csharp
public string GetDisplayName(Dictionary<int, User> users, int id)
{
    if (users.TryGetValue(id, out var user))
    {
        if (user.DisplayName != null)
        {
            return user.DisplayName;
        }
    }

    return "Anonymous";
}
```

### Expected

```csharp
public string GetDisplayName(Dictionary<int, User> users, int id)
{
    if (!users.TryGetValue(id, out var user))
        return "Anonymous";

    if (user.DisplayName is null)
        return "Anonymous";

    return user.DisplayName;
}
```

---

## 28. Enum-Based Branching With Nesting Inside Each Case

### Actual

```csharp
public void HandleMessage(Message msg)
{
    if (msg != null)
    {
        if (msg.Type == MessageType.Text)
        {
            if (!string.IsNullOrEmpty(msg.Body))
            {
                DisplayText(msg.Body);
            }
        }
        else if (msg.Type == MessageType.Image)
        {
            if (msg.Attachment != null)
            {
                DisplayImage(msg.Attachment);
            }
        }
    }
}
```

### Expected

```csharp
public void HandleMessage(Message msg)
{
    if (msg is null)
        return;

    if (msg.Type == MessageType.Text)
    {
        if (!string.IsNullOrEmpty(msg.Body))
        {
            DisplayText(msg.Body);
        }
    }

    if (msg.Type == MessageType.Image)
    {
        if (msg.Attachment != null)
        {
            DisplayImage(msg.Attachment);
        }
    }
}
```

---

## 29. Nested `if` With `out` Variables

### Actual

```csharp
public decimal? ParsePrice(string input)
{
    if (input != null)
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
```

### Expected

```csharp
public decimal? ParsePrice(string input)
{
    if (input is null)
        return null;

    if (!decimal.TryParse(input, out var price))
        return null;

    if (price < 0)
        return null;

    return price;
}
```

---

## 30. Mixed Nesting — `if` Inside `switch`

### Actual

```csharp
public string Process(Command cmd)
{
    switch (cmd.Action)
    {
        case "save":
            if (cmd.Data != null)
            {
                if (cmd.Data.Length < 1_000_000)
                {
                    Save(cmd.Data);
                    return "Saved";
                }
            }
            return "Save failed";

        case "delete":
            if (cmd.TargetId.HasValue)
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
```

### Expected

```csharp
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
            if (!cmd.TargetId.HasValue)
                return "Delete failed";

            if (!Exists(cmd.TargetId.Value))
                return "Delete failed";

            Delete(cmd.TargetId.Value);
            return "Deleted";

        default:
            return "Unknown action";
    }
}
```

---

## 31. Five-Level Deep Nesting — Full Validation Pipeline

### Actual

```csharp
public async Task<Result> SubmitClaimAsync(Claim claim)
{
    if (claim != null)
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
```

### Expected

```csharp
public async Task<Result> SubmitClaimAsync(Claim claim)
{
    if (claim is null)
        return Result.Failure("Claim is null");

    if (claim.PolicyId == Guid.Empty)
        return Result.Failure("Invalid policy ID");

    var policy = await GetPolicyAsync(claim.PolicyId);
    if (policy is null)
        return Result.Failure("Policy not found");

    if (!policy.IsActive)
        return Result.Failure("Policy is inactive");

    if (claim.Amount <= 0 || claim.Amount > policy.MaxCoverage)
        return Result.Failure("Amount out of range");

    var reference = await CreateClaimAsync(claim);
    await NotifyAdjusterAsync(reference);
    return Result.Success(reference);
}
```

---

## 32. Nested Conditions in a Disposal/Cleanup Method

### Actual

```csharp
public void Dispose()
{
    if (!_disposed)
    {
        if (_connection != null)
        {
            if (_connection.State == ConnectionState.Open)
            {
                _connection.Close();
            }
            _connection.Dispose();
            _connection = null;
        }
        _disposed = true;
    }
}
```

### Expected

```csharp
public void Dispose()
{
    if (_disposed)
        return;

    if (_connection is null)
    {
        _disposed = true;
        return;
    }

    if (_connection.State == ConnectionState.Open)
        _connection.Close();

    _connection.Dispose();
    _connection = null;
    _disposed = true;
}
```

---

## 33. Complex Loop — Nested `if` With Accumulator and Early Exit

### Actual

```csharp
public decimal CalculateTotalDiscount(List<CartItem> items, Customer customer)
{
    var total = 0m;
    foreach (var item in items)
    {
        if (item.IsDiscountable)
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
```

### Expected

```csharp
public decimal CalculateTotalDiscount(List<CartItem> items, Customer customer)
{
    var total = 0m;
    foreach (var item in items)
    {
        if (!item.IsDiscountable)
            continue;

        if (customer.MembershipLevel == MembershipLevel.None)
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
```

---

## 34. Nested `if` With Multiple `else` Branches at Different Levels

### Actual

```csharp
public string Evaluate(Submission submission)
{
    if (submission != null)
    {
        if (submission.IsComplete)
        {
            if (submission.Score >= 90)
            {
                return "Excellent";
            }
            else if (submission.Score >= 70)
            {
                return "Good";
            }
            else
            {
                return "Needs improvement";
            }
        }
        else
        {
            return "Incomplete";
        }
    }
    else
    {
        return "No submission";
    }
}
```

### Expected

```csharp
public string Evaluate(Submission submission)
{
    if (submission is null)
        return "No submission";

    if (!submission.IsComplete)
        return "Incomplete";

    if (submission.Score >= 90)
        return "Excellent";

    if (submission.Score >= 70)
        return "Good";

    return "Needs improvement";
}
```

---

## 35. Nested `if` With Intermediate Variables at Each Level

### Actual

```csharp
public string BuildReport(int projectId)
{
    if (projectId > 0)
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
```

### Expected

```csharp
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
```

---

## 36. Nested `if` With `throw` Instead of `return`

### Actual

```csharp
public void RegisterUser(string username, string password)
{
    if (username != null)
    {
        if (password != null)
        {
            if (username.Length >= 3)
            {
                if (password.Length >= 8)
                {
                    var user = new User(username, Hash(password));
                    _store.Save(user);
                }
                else
                {
                    throw new ArgumentException("Password too short");
                }
            }
            else
            {
                throw new ArgumentException("Username too short");
            }
        }
        else
        {
            throw new ArgumentNullException(nameof(password));
        }
    }
    else
    {
        throw new ArgumentNullException(nameof(username));
    }
}
```

### Expected

```csharp
public void RegisterUser(string username, string password)
{
    if (username is null)
        throw new ArgumentNullException(nameof(username));

    if (password is null)
        throw new ArgumentNullException(nameof(password));

    if (username.Length < 3)
        throw new ArgumentException("Username too short");

    if (password.Length < 8)
        throw new ArgumentException("Password too short");

    var user = new User(username, Hash(password));
    _store.Save(user);
}
```

---

## 37. Complex Async Pipeline With Nesting

### Actual

```csharp
public async Task<ApiResponse> ProcessRequestAsync(HttpContext context)
{
    if (context.Request.Method == "POST")
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
```

### Expected

```csharp
public async Task<ApiResponse> ProcessRequestAsync(HttpContext context)
{
    if (context.Request.Method != "POST")
        return new ApiResponse(405, "Method not allowed");

    var token = context.Request.Headers["Authorization"].FirstOrDefault();
    if (token is null)
        return new ApiResponse(401, "Missing token");

    var principal = await _authService.ValidateTokenAsync(token);
    if (principal is null)
        return new ApiResponse(401, "Invalid token");

    if (!principal.HasPermission("write"))
        return new ApiResponse(403, "Forbidden");

    var body = await ReadBodyAsync(context.Request);
    if (body is null)
        return new ApiResponse(400, "Empty body");

    var result = await _handler.HandleAsync(body, principal);
    return new ApiResponse(200, result);
}
```

---

## 38. Nested `if` Inside a `finally` Block (Edge Case)

### Actual

```csharp
public void SafeExecute(Action action, ILogger logger)
{
    try
    {
        action();
    }
    finally
    {
        if (logger != null)
        {
            if (logger.IsEnabled)
            {
                logger.Log("Execution completed");
            }
        }
    }
}
```

### Expected

```csharp
public void SafeExecute(Action action, ILogger logger)
{
    try
    {
        action();
    }
    finally
    {
        if (logger is null)
            return;

        if (!logger.IsEnabled)
            return;

        logger.Log("Execution completed");
    }
}
```

---

## 39. Nested Ternary-Like `if-else` Chain Flattened to Guards

### Actual

```csharp
public Priority DeterminePriority(Ticket ticket)
{
    if (ticket != null)
    {
        if (ticket.Severity == Severity.Critical)
        {
            if (ticket.AffectedUsers > 1000)
            {
                return Priority.Immediate;
            }
            else
            {
                return Priority.High;
            }
        }
        else
        {
            if (ticket.Severity == Severity.Major)
            {
                return Priority.Medium;
            }
            else
            {
                return Priority.Low;
            }
        }
    }
    else
    {
        return Priority.None;
    }
}
```

### Expected

```csharp
public Priority DeterminePriority(Ticket ticket)
{
    if (ticket is null)
        return Priority.None;

    if (ticket.Severity == Severity.Critical)
        return ticket.AffectedUsers > 1000 ? Priority.Immediate : Priority.High;

    if (ticket.Severity == Severity.Major)
        return Priority.Medium;

    return Priority.Low;
}
```

---

## 40. Six-Level Nesting — Full E-Commerce Checkout Pipeline

### Actual

```csharp
public async Task<CheckoutResult> CheckoutAsync(Cart cart, PaymentInfo payment, Address shipping)
{
    if (cart != null)
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
```

### Expected

```csharp
public async Task<CheckoutResult> CheckoutAsync(Cart cart, PaymentInfo payment, Address shipping)
{
    if (cart is null)
        return CheckoutResult.Failure("Cart is null");

    if (cart.Items.Count <= 0)
        return CheckoutResult.Failure("Cart is empty");

    if (payment is null)
        return CheckoutResult.Failure("Payment information required");

    if (!payment.IsValid())
        return CheckoutResult.Failure("Invalid payment information");

    if (shipping is null)
        return CheckoutResult.Failure("Shipping address required");

    if (!await _inventoryService.ReserveAllAsync(cart.Items))
        return CheckoutResult.Failure("Some items are out of stock");

    var total = cart.CalculateTotal();
    var charge = await _paymentGateway.ChargeAsync(payment, total);
    if (!charge.Succeeded)
    {
        await _inventoryService.ReleaseAllAsync(cart.Items);
        return CheckoutResult.Failure($"Payment failed: {charge.Error}");
    }

    var order = await _orderService.CreateAsync(cart, shipping, charge.TransactionId);
    await _emailService.SendConfirmationAsync(order);
    return CheckoutResult.Success(order.Id);
}
```

---

## 41. Nested `if` With `yield return` in an Iterator

### Actual

```csharp
public IEnumerable<string> GetValidEmails(IEnumerable<Contact> contacts)
{
    foreach (var contact in contacts)
    {
        if (contact != null)
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
```

### Expected

```csharp
public IEnumerable<string> GetValidEmails(IEnumerable<Contact> contacts)
{
    foreach (var contact in contacts)
    {
        if (contact is null)
            continue;

        if (!contact.IsActive)
            continue;

        if (string.IsNullOrWhiteSpace(contact.Email))
            continue;

        if (!contact.Email.Contains('@'))
            continue;

        yield return contact.Email;
    }
}
```

---

## 42. Nested `if` Inside a Lambda / Delegate

### Actual

```csharp
public void RegisterCallbacks(EventBus bus)
{
    bus.On<OrderPlaced>(e =>
    {
        if (e.Order != null)
        {
            if (e.Order.Total > 500)
            {
                if (e.Order.Customer.IsVip)
                {
                    SendVipGift(e.Order.Customer);
                    LogVipOrder(e.Order);
                }
            }
        }
    });
}
```

### Expected

```csharp
public void RegisterCallbacks(EventBus bus)
{
    bus.On<OrderPlaced>(e =>
    {
        if (e.Order is null)
            return;

        if (e.Order.Total <= 500)
            return;

        if (!e.Order.Customer.IsVip)
            return;

        SendVipGift(e.Order.Customer);
        LogVipOrder(e.Order);
    });
}
```

---

## 43. Static Method — Combined Guard and Business Logic

### Actual

```csharp
public static TimeSpan? CalculateEstimate(Project project, Team team)
{
    if (project != null)
    {
        if (team != null)
        {
            if (team.Members.Count > 0)
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
```

### Expected

```csharp
public static TimeSpan? CalculateEstimate(Project project, Team team)
{
    if (project is null)
        return null;

    if (team is null)
        return null;

    if (team.Members.Count <= 0)
        return null;

    if (project.StoryPoints <= 0)
        return null;

    var velocity = team.GetAverageVelocity();
    if (velocity <= 0)
        return null;

    var sprints = (double)project.StoryPoints / velocity;
    return TimeSpan.FromDays(sprints * team.SprintLengthDays);
}
```

---

## 44. Nested `if` With `break` in a Labelled/Search Loop

### Actual

```csharp
public User? FindFirstAdmin(List<Department> departments)
{
    User? result = null;
    foreach (var dept in departments)
    {
        if (dept.IsActive)
        {
            foreach (var user in dept.Users)
            {
                if (user.Role == Role.Admin)
                {
                    if (user.IsActive)
                    {
                        result = user;
                        break;
                    }
                }
            }

            if (result != null)
                break;
        }
    }
    return result;
}
```

### Expected

```csharp
public User? FindFirstAdmin(List<Department> departments)
{
    foreach (var dept in departments)
    {
        if (!dept.IsActive)
            continue;

        foreach (var user in dept.Users)
        {
            if (user.Role != Role.Admin)
                continue;

            if (!user.IsActive)
                continue;

            return user;
        }
    }

    return null;
}
```

---

## 45. Complex Middleware-Style Pipeline With Nesting

### Actual

```csharp
public async Task<Response> HandleAsync(Request request, CancellationToken ct)
{
    if (!ct.IsCancellationRequested)
    {
        if (request.Headers.TryGetValue("X-Api-Key", out var apiKey))
        {
            if (await _rateLimiter.TryAcquireAsync(apiKey, ct))
            {
                var tenant = await _tenantResolver.ResolveAsync(apiKey, ct);
                if (tenant != null)
                {
                    if (tenant.IsEnabled)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
                        if (handler != null)
                        {
                            var context = new RequestContext(request, tenant, scope);
                            var result = await handler.ExecuteAsync(context, ct);
                            await _metrics.RecordAsync(tenant.Id, result.StatusCode);
                            return result;
                        }
                        else
                        {
                            return Response.Error(500, "Handler not resolved");
                        }
                    }
                    else
                    {
                        return Response.Error(403, "Tenant disabled");
                    }
                }
                else
                {
                    return Response.Error(401, "Unknown tenant");
                }
            }
            else
            {
                return Response.Error(429, "Rate limit exceeded");
            }
        }
        else
        {
            return Response.Error(401, "API key missing");
        }
    }
    else
    {
        return Response.Error(499, "Request cancelled");
    }
}
```

### Expected

```csharp
public async Task<Response> HandleAsync(Request request, CancellationToken ct)
{
    if (ct.IsCancellationRequested)
        return Response.Error(499, "Request cancelled");

    if (!request.Headers.TryGetValue("X-Api-Key", out var apiKey))
        return Response.Error(401, "API key missing");

    if (!await _rateLimiter.TryAcquireAsync(apiKey, ct))
        return Response.Error(429, "Rate limit exceeded");

    var tenant = await _tenantResolver.ResolveAsync(apiKey, ct);
    if (tenant is null)
        return Response.Error(401, "Unknown tenant");

    if (!tenant.IsEnabled)
        return Response.Error(403, "Tenant disabled");

    using var scope = _serviceProvider.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler>();
    if (handler is null)
        return Response.Error(500, "Handler not resolved");

    var context = new RequestContext(request, tenant, scope);
    var result = await handler.ExecuteAsync(context, ct);
    await _metrics.RecordAsync(tenant.Id, result.StatusCode);
    return result;
}
```
